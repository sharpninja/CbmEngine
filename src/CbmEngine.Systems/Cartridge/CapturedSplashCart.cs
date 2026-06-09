using CbmEngine.Systems.Audio;

namespace CbmEngine.Systems.Cartridge;

public sealed record CapturedSplashAssets(
    byte[] Charset2048,
    byte[] Screen1000,
    byte[] Color1000,
    byte[] Sprites832,
    byte[] SpritePointers8,
    byte[] SpriteXY16,
    byte SpriteEnable,
    byte SpriteMulticolor,
    byte SpriteMc1,
    byte SpriteMc2,
    byte[] SpriteColors8,
    byte D011,
    byte D016,
    byte D018,
    byte BgColor,
    byte BorderColor);

public static class CapturedSplashCart
{
    public const string LinkerConfig = """
        MEMORY {
            CART: start = $8000, size = $4000, type = ro, fill = yes, fillval = $00, file = %O;
        }
        SEGMENTS {
            HEADER:  load = CART, type = ro, start = $8000;
            BOOT:    load = CART, type = ro, start = $8009;
            IRQ:     load = CART, type = ro, start = $8400;
            CHARSET: load = CART, type = ro, start = $8500;
            SCREEN:  load = CART, type = ro, start = $8D00;
            COLOR:   load = CART, type = ro, start = $90E8;
            SPRITES: load = CART, type = ro, start = $94D0;
            PAYLOAD: load = CART, type = ro, start = $A010;
        }
        """;

    public static byte[] Build(PsidProgram program, CapturedSplashAssets assets, Ca65Assembler? assembler = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(assets);
        if (assets.Charset2048.Length != 2048) throw new ArgumentException("charset must be 2048 bytes");
        if (assets.Screen1000.Length != 1000) throw new ArgumentException("screen must be 1000 bytes");
        if (assets.Color1000.Length != 1000) throw new ArgumentException("color must be 1000 bytes");
        if (assets.Sprites832.Length is not (512 or 832 or 2048)) throw new ArgumentException("sprites must be 512, 832, or 2048 bytes");
        if (assets.SpritePointers8.Length != 8) throw new ArgumentException("sprite pointers must be 8 bytes");
        if (assets.SpriteXY16.Length != 16) throw new ArgumentException("sprite XY must be 16 bytes");
        if (assets.SpriteColors8.Length != 8) throw new ArgumentException("sprite colors must be 8 bytes");
        if (program.Payload.Length > 5400) throw new ArgumentException($"PSID payload {program.Payload.Length} exceeds available space.");

        assembler ??= new Ca65Assembler();
        string source = BuildSource(program, assets);
        var includes = new Dictionary<string, byte[]>
        {
            ["psid_payload.bin"] = program.Payload.ToArray(),
            ["charset.bin"] = assets.Charset2048,
            ["screen.bin"] = assets.Screen1000,
            ["color.bin"] = assets.Color1000,
            ["sprites.bin"] = assets.Sprites832,
        };
        return assembler.Build(source, LinkerConfig, includes);
    }

    private static string BuildSource(PsidProgram program, CapturedSplashAssets a)
    {
        ushort loadAddr = program.Header.LoadAddress;
        ushort initAddr = program.Header.InitAddress;
        ushort playAddr = program.Header.PlayAddress;
        int psidLen = program.Payload.Length;

        string spritePtrBytes = string.Join(",", a.SpritePointers8.Select(b => $"${b:X2}"));
        string spriteXyBytes = string.Join(",", a.SpriteXY16.Select(b => $"${b:X2}"));
        string spriteColorBytes = string.Join(",", a.SpriteColors8.Select(b => $"${b:X2}"));

        return $$"""
            PSID_LOAD       = ${{loadAddr:X4}}
            PSID_INIT       = ${{initAddr:X4}}
            PSID_PLAY       = ${{playAddr:X4}}
            PSID_LEN        = ${{psidLen:X4}}
            MARKER_HI_VAL   = ${{BootstrapCart.MarkerHi:X2}}
            MARKER_LO_VAL   = ${{BootstrapCart.MarkerLo:X2}}
            MARKER_ADDR     = ${{BootstrapCart.MarkerAddress:X4}}
            BG_COLOR        = ${{a.BgColor:X2}}
            BORDER_COLOR    = ${{a.BorderColor:X2}}
            VIC_D011        = ${{a.D011:X2}}
            VIC_D016        = ${{a.D016:X2}}
            VIC_D018        = ${{a.D018:X2}}
            SPR_ENABLE      = ${{a.SpriteEnable:X2}}
            SPR_MC          = ${{a.SpriteMulticolor:X2}}
            SPR_MC1         = ${{a.SpriteMc1:X2}}
            SPR_MC2         = ${{a.SpriteMc2:X2}}

            ZP_SRC_LO       = $FB
            ZP_SRC_HI       = $FC
            ZP_DST_LO       = $FD
            ZP_DST_HI       = $FE
            ZP_COUNT_LO     = $F9
            ZP_COUNT_HI     = $FA

            CART_CHARSET    = $8500
            CART_SCREEN     = $8D00
            CART_COLOR      = $90E8
            CART_SPRITES    = $94D0
            CART_PAYLOAD    = $A010
            RAM_CHARSET     = $3000
            RAM_SCREEN      = $0400
            RAM_COLOR       = $D800
            RAM_SPRITES     = $3800
            IRQ_HANDLER     = $8400
            KERNAL_IRQ_EXIT = $EA81

            .segment "HEADER"
                    .addr   cold_start
                    .addr   cold_start
                    .byte   $C3, $C2, $CD, $38, $30

            .segment "BOOT"
            cold_start:
                    sei
                    ldx     #$FF
                    txs
                    cld
                    lda     #$37
                    sta     $01

                    ; Blank screen during setup
                    lda     #$0B
                    sta     $D011

                    ; Copy charset -> $3000 (2048 bytes)
                    lda     #<CART_CHARSET
                    sta     ZP_SRC_LO
                    lda     #>CART_CHARSET
                    sta     ZP_SRC_HI
                    lda     #<RAM_CHARSET
                    sta     ZP_DST_LO
                    lda     #>RAM_CHARSET
                    sta     ZP_DST_HI
                    lda     #<2048
                    sta     ZP_COUNT_LO
                    lda     #>2048
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Copy screen RAM -> $0400 (1000 bytes)
                    lda     #<CART_SCREEN
                    sta     ZP_SRC_LO
                    lda     #>CART_SCREEN
                    sta     ZP_SRC_HI
                    lda     #<RAM_SCREEN
                    sta     ZP_DST_LO
                    lda     #>RAM_SCREEN
                    sta     ZP_DST_HI
                    lda     #<1000
                    sta     ZP_COUNT_LO
                    lda     #>1000
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Copy color RAM -> $D800 (1000 bytes)
                    lda     #<CART_COLOR
                    sta     ZP_SRC_LO
                    lda     #>CART_COLOR
                    sta     ZP_SRC_HI
                    lda     #<RAM_COLOR
                    sta     ZP_DST_LO
                    lda     #>RAM_COLOR
                    sta     ZP_DST_HI
                    lda     #<1000
                    sta     ZP_COUNT_LO
                    lda     #>1000
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Copy sprite blocks -> RAM_SPRITES (512 bytes covering 8 sprite blocks)
                    lda     #<CART_SPRITES
                    sta     ZP_SRC_LO
                    lda     #>CART_SPRITES
                    sta     ZP_SRC_HI
                    lda     #<RAM_SPRITES
                    sta     ZP_DST_LO
                    lda     #>RAM_SPRITES
                    sta     ZP_DST_HI
                    lda     #<512
                    sta     ZP_COUNT_LO
                    lda     #>512
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Install sprite pointers in screen RAM $07F8-$07FF
                    ldx     #7
            cpsptr:
                    lda     sprite_pointers,x
                    sta     $07F8,x
                    dex
                    bpl     cpsptr

                    ; Copy PSID payload to load address
                    lda     #<CART_PAYLOAD
                    sta     ZP_SRC_LO
                    lda     #>CART_PAYLOAD
                    sta     ZP_SRC_HI
                    lda     #<PSID_LOAD
                    sta     ZP_DST_LO
                    lda     #>PSID_LOAD
                    sta     ZP_DST_HI
                    lda     #<PSID_LEN
                    sta     ZP_COUNT_LO
                    lda     #>PSID_LEN
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Run PSID init
                    lda     #$00
                    jsr     PSID_INIT

                    ; Sprite X/Y positions from captured state
                    ldx     #15
            cpsxy:
                    lda     sprite_xy,x
                    sta     $D000,x
                    dex
                    bpl     cpsxy

                    lda     #$00
                    sta     $D010                     ; sprite X MSB
                    lda     #SPR_ENABLE
                    sta     $D015
                    lda     #SPR_MC
                    sta     $D01C
                    lda     #SPR_MC1
                    sta     $D025
                    lda     #SPR_MC2
                    sta     $D026

                    ldx     #7
            cpscol:
                    lda     sprite_colors,x
                    sta     $D027,x
                    dex
                    bpl     cpscol

                    ; Border + background
                    lda     #BORDER_COLOR
                    sta     $D020
                    lda     #BG_COLOR
                    sta     $D021

                    ; Text-mode VIC config (turn screen ON)
                    lda     #VIC_D016
                    sta     $D016
                    lda     #VIC_D018
                    sta     $D018
                    lda     #VIC_D011
                    sta     $D011

                    ; IRQ vector to our play handler
                    sei
                    lda     #<IRQ_HANDLER
                    sta     $0314
                    lda     #>IRQ_HANDLER
                    sta     $0315

                    ; CIA1 timer A for ~PAL refresh
                    lda     #$C7
                    sta     $DC04
                    lda     #$4C
                    sta     $DC05
                    lda     #$7F
                    sta     $DC0D
                    lda     #$81
                    sta     $DC0D
                    lda     #$11
                    sta     $DC0E

                    ; Engine handoff marker
                    lda     #MARKER_HI_VAL
                    sta     MARKER_ADDR
                    lda     #MARKER_LO_VAL
                    sta     MARKER_ADDR + 1

                    cli
            park:
                    jmp     park

            ; --- copy_block: src=$FB/$FC, dst=$FD/$FE, count=$F9/$FA ---
            copy_block:
                    ldy     #$00
            cb_loop:
                    lda     (ZP_SRC_LO),y
                    sta     (ZP_DST_LO),y
                    inc     ZP_SRC_LO
                    bne     :+
                    inc     ZP_SRC_HI
            :
                    inc     ZP_DST_LO
                    bne     :+
                    inc     ZP_DST_HI
            :
                    lda     ZP_COUNT_LO
                    bne     :+
                    dec     ZP_COUNT_HI
            :
                    dec     ZP_COUNT_LO
                    lda     ZP_COUNT_LO
                    ora     ZP_COUNT_HI
                    bne     cb_loop
                    rts

            sprite_pointers:
                    .byte   {{spritePtrBytes}}
            sprite_xy:
                    .byte   {{spriteXyBytes}}
            sprite_colors:
                    .byte   {{spriteColorBytes}}

            .segment "IRQ"
            irq_handler:
                    lda     #$01
                    sta     $D019
                    lda     $DC0D
                    jsr     PSID_PLAY
                    jmp     KERNAL_IRQ_EXIT

            .segment "CHARSET"
                    .incbin "charset.bin"

            .segment "SCREEN"
                    .incbin "screen.bin"

            .segment "COLOR"
                    .incbin "color.bin"

            .segment "SPRITES"
                    .incbin "sprites.bin"

            .segment "PAYLOAD"
                    .incbin "psid_payload.bin"
            """;
    }
}

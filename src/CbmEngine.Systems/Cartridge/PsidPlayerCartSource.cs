using CbmEngine.Pipeline;
using CbmEngine.Systems.Audio;

namespace CbmEngine.Systems.Cartridge;

public static class PsidPlayerCartSource
{
    public const string LinkerConfig = """
        MEMORY {
            CART: start = $8000, size = $4000, type = ro, fill = yes, fillval = $00, file = %O;
        }
        SEGMENTS {
            HEADER:  load = CART, type = ro, start = $8000;
            BOOT:    load = CART, type = ro, start = $8009;
            IRQ:     load = CART, type = ro, start = $8200;
            BITMAP:  load = CART, type = ro, start = $8300;
            SCREEN:  load = CART, type = ro, start = $A240;
            COLOR:   load = CART, type = ro, start = $A628;
            PAYLOAD: load = CART, type = ro, start = $AA10;
        }
        """;

    public const string PayloadFileName = "psid_payload.bin";
    public const string BitmapFileName  = "splash_bitmap.bin";
    public const string ScreenFileName  = "splash_screen.bin";
    public const string ColorFileName   = "splash_color.bin";

    public static string BuildSource(
        PsidProgram program,
        byte backgroundColor,
        byte initialBorderColor,
        int borderCyclePeriodFrames,
        EncodedSplashBitmap? splash)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (borderCyclePeriodFrames < 1 || borderCyclePeriodFrames > 255) throw new ArgumentOutOfRangeException(nameof(borderCyclePeriodFrames));

        int length = program.Payload.Length;
        ushort loadAddr = program.Header.LoadAddress;
        ushort initAddr = program.Header.InitAddress;
        ushort playAddr = program.Header.PlayAddress;
        int startSong = program.Header.StartSong > 0 ? program.Header.StartSong - 1 : 0;
        byte splashBg = splash?.BackgroundColorIndex ?? backgroundColor;
        int hasSplash = splash is not null ? 1 : 0;

        return $$"""
            ; CbmEngine PSID player cart - assembled by CA65/LD65.
            ; Optionally embeds a 160x200 multicolor bitmap splash that displays while music plays.

            PSID_LOAD       = ${{loadAddr:X4}}
            PSID_INIT       = ${{initAddr:X4}}
            PSID_PLAY       = ${{playAddr:X4}}
            PSID_LEN        = ${{length:X4}}
            START_SONG      = ${{startSong:X2}}
            BG_COLOR        = ${{backgroundColor:X2}}
            BORDER_INIT     = ${{initialBorderColor:X2}}
            BORDER_PERIOD   = ${{borderCyclePeriodFrames:X2}}
            SPLASH_BG       = ${{splashBg:X2}}
            HAS_SPLASH      = ${{hasSplash}}
            MARKER_HI_VAL   = ${{BootstrapCart.MarkerHi:X2}}
            MARKER_LO_VAL   = ${{BootstrapCart.MarkerLo:X2}}
            MARKER_ADDR     = ${{BootstrapCart.MarkerAddress:X4}}

            ZP_SRC_LO       = $FB
            ZP_SRC_HI       = $FC
            ZP_DST_LO       = $FD
            ZP_DST_HI       = $FE
            ZP_COUNT_LO     = $F9
            ZP_COUNT_HI     = $FA
            ZP_BORDER_TICK  = $36

            EMBEDDED_PSID   = $AA10
            EMBEDDED_BITMAP = $8300
            EMBEDDED_SCREEN = $A240
            EMBEDDED_COLOR  = $A628
            RAM_BITMAP      = $6000
            RAM_SCREEN      = $4400
            RAM_COLOR       = $D800
            IRQ_HANDLER     = $8200
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

                    lda     #$00
                    sta     ZP_BORDER_TICK

            .if HAS_SPLASH
                    ; Copy bitmap data from cart ROM to RAM at $2000 (8000 bytes)
                    lda     #<EMBEDDED_BITMAP
                    sta     ZP_SRC_LO
                    lda     #>EMBEDDED_BITMAP
                    sta     ZP_SRC_HI
                    lda     #<RAM_BITMAP
                    sta     ZP_DST_LO
                    lda     #>RAM_BITMAP
                    sta     ZP_DST_HI
                    lda     #<8000
                    sta     ZP_COUNT_LO
                    lda     #>8000
                    sta     ZP_COUNT_HI
                    jsr     copy_block

                    ; Copy screen RAM data ($A140) to $0400 (1000 bytes)
                    lda     #<EMBEDDED_SCREEN
                    sta     ZP_SRC_LO
                    lda     #>EMBEDDED_SCREEN
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

                    ; Copy color RAM data ($A528) to $D800 (1000 bytes)
                    lda     #<EMBEDDED_COLOR
                    sta     ZP_SRC_LO
                    lda     #>EMBEDDED_COLOR
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
            .else
                    ; No splash: clear screen RAM ($0400..$07E7) to space ($20)
                    ldx     #$00
                    lda     #$20
            clr_screen:
                    sta     $0400,x
                    sta     $0500,x
                    sta     $0600,x
                    sta     $0700,x
                    inx
                    bne     clr_screen
                    ldx     #$00
                    lda     #BG_COLOR
            clr_color:
                    sta     $D800,x
                    sta     $D900,x
                    sta     $DA00,x
                    sta     $DB00,x
                    inx
                    bne     clr_color
            .endif

                    ; Copy PSID payload from cart ROM at EMBEDDED_PSID to RAM at PSID_LOAD
                    lda     #<EMBEDDED_PSID
                    sta     ZP_SRC_LO
                    lda     #>EMBEDDED_PSID
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

                    ; Call PSID init with A = song number
                    lda     #START_SONG
                    jsr     PSID_INIT

            .if HAS_SPLASH
                    ; Switch VIC to bank 1 ($4000-$7FFF) so bitmap at $6000 doesn't conflict with PSID at $0FFF..$2487
                    lda     $DD02
                    ora     #$03
                    sta     $DD02
                    lda     $DD00
                    and     #$FC
                    ora     #$02
                    sta     $DD00
                    ; Bitmap mode: $D011 BMM=1 DEN=1 RSEL=1 scroll Y=3
                    lda     #$3B
                    sta     $D011
                    ; $D016 MCM={{(splash?.Mode == SplashBitmapMode.HiRes ? "0" : "1")}} CSEL=1 scroll X=0
                    lda     #${{(splash?.Mode == SplashBitmapMode.HiRes ? "C8" : "D8")}}
                    sta     $D016
                    ; $D018: screen base = 1 ($4400 within bank 1), bitmap base = 4 ($6000 within bank 1)
                    lda     #$18
                    sta     $D018
                    lda     #SPLASH_BG
                    sta     $D021
            .else
                    lda     #$1B
                    sta     $D011
                    lda     #$C8
                    sta     $D016
                    lda     #$14
                    sta     $D018
                    lda     #BG_COLOR
                    sta     $D021
            .endif
                    lda     #BORDER_INIT
                    sta     $D020

                    ; Install our IRQ vector
                    sei
                    lda     #<IRQ_HANDLER
                    sta     $0314
                    lda     #>IRQ_HANDLER
                    sta     $0315

                    ; CIA1 timer A for PAL refresh
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
            copy_block_loop:
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
                    bne     copy_block_loop
                    rts

            .segment "IRQ"
            irq_handler:
                    lda     #$01
                    sta     $D019
                    lda     $DC0D
                    jsr     PSID_PLAY
                    inc     ZP_BORDER_TICK
                    lda     ZP_BORDER_TICK
                    cmp     #BORDER_PERIOD
                    bne     skip_border
                    lda     #$00
                    sta     ZP_BORDER_TICK
                    inc     $D020
            skip_border:
                    jmp     KERNAL_IRQ_EXIT

            .segment "BITMAP"
            .if HAS_SPLASH
                    .incbin "{{BitmapFileName}}"
            .endif

            .segment "SCREEN"
            .if HAS_SPLASH
                    .incbin "{{ScreenFileName}}"
            .endif

            .segment "COLOR"
            .if HAS_SPLASH
                    .incbin "{{ColorFileName}}"
            .endif

            .segment "PAYLOAD"
                    .incbin "{{PayloadFileName}}"
            """;
    }
}

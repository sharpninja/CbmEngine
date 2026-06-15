using CbmEngine.Pipeline;

namespace CbmEngine.Systems.Cartridge;

public static class BitmapPlayerCartSource
{
    public const string LinkerConfig = """
        MEMORY {
            CART: start = $8000, size = $4000, type = ro, fill = yes, fillval = $00, file = %O;
        }
        SEGMENTS {
            HEADER:  load = CART, type = ro, start = $8000;
            BOOT:    load = CART, type = ro, start = $8009;
            BITMAP:  load = CART, type = ro, start = $8300;
            SCREEN:  load = CART, type = ro, start = $A240;
            COLOR:   load = CART, type = ro, start = $A628;
        }
        """;

    public const string BitmapFileName = "splash_bitmap.bin";
    public const string ScreenFileName = "splash_screen.bin";
    public const string ColorFileName = "splash_color.bin";

    public static string BuildSource(EncodedSplashBitmap splash)
    {
        ArgumentNullException.ThrowIfNull(splash);
        byte d016 = splash.Mode == SplashBitmapMode.HiRes ? (byte)0xC8 : (byte)0xD8;

        return $$"""
            BG_COLOR        = ${{splash.BackgroundColorIndex:X2}}
            VIC_D016        = ${{d016:X2}}
            MARKER_HI_VAL   = ${{BootstrapCart.MarkerHi:X2}}
            MARKER_LO_VAL   = ${{BootstrapCart.MarkerLo:X2}}
            MARKER_ADDR     = ${{BootstrapCart.MarkerAddress:X4}}

            ZP_SRC_LO       = $FB
            ZP_SRC_HI       = $FC
            ZP_DST_LO       = $FD
            ZP_DST_HI       = $FE
            ZP_COUNT_LO     = $F9
            ZP_COUNT_HI     = $FA

            EMBEDDED_BITMAP = $8300
            EMBEDDED_SCREEN = $A240
            EMBEDDED_COLOR  = $A628
            RAM_BITMAP      = $6000
            RAM_SCREEN      = $4400
            RAM_COLOR       = $D800

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

                    ; Bitmap (8000 bytes) -> $6000
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

                    ; Screen (1000 bytes) -> $4400
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

                    ; Color (1000 bytes) -> $D800
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

                    ; Switch VIC to bank 1 ($4000-$7FFF) so bitmap at $6000 is visible
                    lda     $DD02
                    ora     #$03
                    sta     $DD02
                    lda     $DD00
                    and     #$FC
                    ora     #$02
                    sta     $DD00

                    ; Bitmap mode + DEN + RSEL + scroll Y=3
                    lda     #$3B
                    sta     $D011
                    ; MCM bit per splash mode + CSEL + scroll X=0
                    lda     #VIC_D016
                    sta     $D016
                    ; Screen base = $4400, bitmap base = $6000 (within bank 1)
                    lda     #$18
                    sta     $D018
                    ; Background color
                    lda     #BG_COLOR
                    sta     $D021
                    ; Border color black
                    lda     #$00
                    sta     $D020

                    ; Deposit engine handoff marker
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

            .segment "BITMAP"
                    .incbin "{{BitmapFileName}}"

            .segment "SCREEN"
                    .incbin "{{ScreenFileName}}"

            .segment "COLOR"
                    .incbin "{{ColorFileName}}"
            """;
    }
}

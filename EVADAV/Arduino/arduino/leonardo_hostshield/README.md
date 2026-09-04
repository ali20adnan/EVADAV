# Arduino Leonardo + USB Host Shield (EVADAV)

Mouse output for EVADAV's **Arduino Leonardo (Host Shield)** method.

## Hardware

1. Arduino Leonardo
2. USB Host Shield 2.0 stacked on the Leonardo (5V jumper)
3. Leonardo USB cable → this PC
4. Real mouse USB cable → Host Shield USB port (not the PC)

## Flash

From the parent `EVADAV` folder, run `UploadMouseSketch.bat`.

Or in Arduino IDE:

1. Tools → Board → **Arduino Leonardo**
2. Sketch → Include Library → Manage Libraries → install **USB Host Shield Library 2.0**
3. Open `leonardo_hostshield.ino`
4. Select the Leonardo COM port
5. Upload

On-board LED on = Host Shield init succeeded.

## After flashing

1. Mouse into the Host Shield USB-A port (not the PC)
2. Leonardo USB into the PC
3. EVADAV → Aim Config → Mouse Movement Method → **Arduino Leonardo (Host Shield)**
4. **Test Arduino Move**

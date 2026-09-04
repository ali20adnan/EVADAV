# EVADAV — Arduino Leonardo mouse method

This folder is the Arduino side of EVADAV.

Aim Config → **Mouse Movement Method** → **Arduino Leonardo (Host Shield)**

The PC sends `dx,dy` over serial. The Leonardo turns those into HID mouse reports. Your real mouse stays usable because it is plugged into the USB Host Shield, not the PC.

## Hardware

1. Arduino Leonardo (ATmega32U4)
2. USB Host Shield 2.0 stacked on the Leonardo (**5V jumper**, ICSP seated)
3. Leonardo USB → this PC
4. Real mouse USB → Host Shield USB-A (not the PC)

## Flash once

1. Install [Arduino CLI](https://arduino.github.io/arduino-cli/) or Arduino IDE
2. In Arduino IDE: Sketch → Include Library → Manage Libraries → install **USB Host Shield Library 2.0**
3. Double-click `UploadMouseSketch.bat` in this folder

Or in Arduino IDE: board = **Arduino Leonardo**, open `arduino/leonardo_hostshield/leonardo_hostshield.ino`, upload.

On-board LED:

- Solid = Host Shield + mouse reports
- Slow blink = shield OK, waiting for a mouse
- Fast blink = shield SPI failed (seat ICSP, 5V jumper)

## Administrator (required)

EVADAV **runs as Administrator by default** (UAC prompt on launch). Arduino mouse only works elevated — COM access and HID inject both need it.

If you skipped UAC, close EVADAV, right-click `EVADAV.exe` → **Run as administrator**.

## In EVADAV

1. Accept the Administrator prompt
2. Aim Config → Mouse Movement Method → **Arduino Leonardo (Host Shield)**
3. Arduino COM Port → **AUTO** (or the COM Windows assigned)
4. Click **Test Arduino Move** — cursor should jump ~120px
5. Use aim assist as usual

Windows should show an Arduino Leonardo mouse plus a COM port (`VID_2341 PID_8036`).

## Serial protocol (already in the sketch)

| Line | Meaning |
|------|---------|
| `PING` / `STAT` | Reply starts with `LEONARDO` |
| `dx,dy` | Relative HID move |
| `TEST` | One 120px jump |
| `CLICK` / `LDOWN` / `LUP` | Left button |

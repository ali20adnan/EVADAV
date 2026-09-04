/*
  EVADAV — Arduino Leonardo + USB Host Shield 2.0
  Real mouse passthrough + serial inject (dx,dy / PING / TEST / CLICK).

  Leonardo USB -> PC
  Mouse USB    -> Host Shield USB-A (NOT the PC)
*/
#ifndef USBCON
#error Select Tools → Board → Arduino Leonardo.
#endif

#include <SPI.h>
#include <usbhub.h>
#include <hidboot.h>
#include <hidcomposite.h>
#include <Mouse.h>

USB Usb;
USBHub Hub(&Usb);

uint8_t gButtons = 0;
uint8_t gLastRlen = 0;
uint16_t gHostMoves = 0;
uint16_t gInjMoves = 0;
uint16_t gVid = 0;
uint16_t gPid = 0;
bool gHostOk = false;
bool gSawMouse = false;
uint8_t gRptOff = 0;
bool gRptOffKnown = false;
uint8_t gIdCandidate = 0;
uint8_t gIdStable = 0;
bool gSawByte1Zero = false;
bool gSawByte1Left = false;

char gLine[64];
uint8_t gLineLen = 0;
uint32_t gLastRxMs = 0;
bool gHaveBytes = false;

void applyButtons(uint8_t buttons)
{
  buttons &= 0x07;
  uint8_t changed = buttons ^ gButtons;
  if (!changed)
    return;
  if (changed & 0x01)
    (buttons & 0x01) ? Mouse.press(MOUSE_LEFT) : Mouse.release(MOUSE_LEFT);
  if (changed & 0x02)
    (buttons & 0x02) ? Mouse.press(MOUSE_RIGHT) : Mouse.release(MOUSE_RIGHT);
  if (changed & 0x04)
    (buttons & 0x04) ? Mouse.press(MOUSE_MIDDLE) : Mouse.release(MOUSE_MIDDLE);
  gButtons = buttons;
}

void hidMove(int16_t x, int16_t y, int8_t wheel)
{
  int8_t sx = (int8_t)constrain(x, -127, 127);
  int8_t sy = (int8_t)constrain(y, -127, 127);
  Mouse.move(sx, sy, wheel);
}

void learnReportOffset(uint8_t len, const uint8_t *buf)
{
  if (gRptOffKnown)
    return;
  if (len < 7)
  {
    gRptOff = 0;
    if (len >= 3)
      gRptOffKnown = true;
    return;
  }
  if (buf[0] == 0)
  {
    gRptOff = 0;
    gRptOffKnown = true;
    return;
  }
  if (buf[0] >= 1 && buf[0] <= 8)
  {
    if (gIdCandidate == 0)
      gIdCandidate = buf[0];
    if (buf[0] == gIdCandidate)
    {
      if (gIdStable < 255)
        gIdStable++;
      if ((buf[1] & 0x01) == 0)
        gSawByte1Zero = true;
      if (buf[1] & 0x01)
        gSawByte1Left = true;
      if (gIdStable >= 10 && gSawByte1Zero && gSawByte1Left)
      {
        gRptOff = 1;
        gRptOffKnown = true;
      }
    }
  }
}

void decodeMouse(uint8_t len, uint8_t *buf, uint8_t *buttons, int16_t *dx, int16_t *dy, int8_t *wheel)
{
  *buttons = 0;
  *dx = 0;
  *dy = 0;
  *wheel = 0;
  if (buf == nullptr || len < 3)
    return;

  learnReportOffset(len, buf);
  uint8_t off = gRptOffKnown ? gRptOff : 0;
  if (off >= len)
    off = 0;

  uint8_t n = (uint8_t)(len - off);
  *buttons = buf[off] & 0x1F;
  if (n >= 6)
  {
    int16_t x16 = (int16_t)(buf[off + 1] | ((uint16_t)buf[off + 2] << 8));
    int16_t y16 = (int16_t)(buf[off + 3] | ((uint16_t)buf[off + 4] << 8));
    if (abs(x16) <= 2048 && abs(y16) <= 2048)
    {
      *dx = x16;
      *dy = y16;
      *wheel = (int8_t)buf[off + 5];
      return;
    }
  }
  *dx = (int8_t)buf[off + 1];
  *dy = (int8_t)buf[off + 2];
  if (n > 3)
    *wheel = (int8_t)buf[off + 3];
}

void onHostReport(uint8_t len, uint8_t *buf)
{
  if (buf == nullptr || len < 3)
    return;
  uint8_t buttons;
  int16_t dx, dy;
  int8_t wheel;
  decodeMouse(len, buf, &buttons, &dx, &dy, &wheel);
  gSawMouse = true;
  gHostMoves++;
  gLastRlen = len;
  applyButtons(buttons);
  hidMove(dx, dy, wheel);
}

class BootParser : public MouseReportParser
{
public:
  void Parse(USBHID * /*hid*/, bool /*is_rpt_id*/, uint8_t len, uint8_t *buf)
  {
    onHostReport(len, buf);
  }
};

class BootMouseDriver : public HIDBoot<USB_HID_PROTOCOL_MOUSE>
{
public:
  BootMouseDriver(USB *usb) : HIDBoot<USB_HID_PROTOCOL_MOUSE>(usb, true) {}

  bool DEVCLASSOK(uint8_t klass) override
  {
    return klass == USB_CLASS_HID || klass == 0 || klass == USB_CLASS_MISC;
  }
  bool DEVSUBCLASSOK(uint8_t) override { return true; }
};

class CompMouseDriver : public HIDComposite
{
public:
  CompMouseDriver(USB *usb) : HIDComposite(usb) {}

  bool DEVCLASSOK(uint8_t klass) override
  {
    return klass == USB_CLASS_HID || klass == 0 || klass == USB_CLASS_MISC;
  }
  bool DEVSUBCLASSOK(uint8_t) override { return true; }

  bool SelectInterface(uint8_t /*iface*/, uint8_t proto) override
  {
    return proto == USB_HID_PROTOCOL_MOUSE;
  }

protected:
  uint8_t OnInitSuccessful() override
  {
    gVid = VID;
    gPid = PID;
    for (uint8_t i = 0; i < 5; i++)
    {
      SetProtocol(i, HID_RPT_PROTOCOL);
      SetIdle(i, 0, 0);
    }
    return 0;
  }

  void ParseHIDData(USBHID * /*hid*/, uint8_t /*ep*/, bool /*is_rpt_id*/, uint8_t len, uint8_t *buf) override
  {
    onHostReport(len, buf);
  }
};

BootMouseDriver HidBootMouse(&Usb);
CompMouseDriver HidComp(&Usb);
BootParser gBootParser;

void replyPing()
{
  bool ready = HidBootMouse.isReady() || HidComp.isReady();
  Serial.print(F("LEONARDO HS="));
  Serial.print(gHostOk ? F("1") : F("0"));
  Serial.print(F(" MOUSE="));
  Serial.print(gSawMouse ? F("1") : F("0"));
  Serial.print(F(" RDY="));
  Serial.print(ready ? F("1") : F("0"));
  Serial.print(F(" U="));
  Serial.print(Usb.getUsbTaskState(), HEX);
  Serial.print(F(" VID="));
  Serial.print(gVid, HEX);
  Serial.print(F(" PID="));
  Serial.print(gPid, HEX);
  Serial.print(F(" HMOV="));
  Serial.print(gHostMoves);
  Serial.print(F(" IMOV="));
  Serial.print(gInjMoves);
  Serial.print(F(" RLEN="));
  Serial.println(gLastRlen);
}

bool ieq(const char *a, const char *b)
{
  while (*a && *b)
  {
    char ca = *a++;
    char cb = *b++;
    if (ca >= 'a' && ca <= 'z') ca = (char)(ca - 32);
    if (cb >= 'a' && cb <= 'z') cb = (char)(cb - 32);
    if (ca != cb) return false;
  }
  return *a == *b;
}

void handleLine(char *line)
{
  if (line == nullptr || line[0] == 0)
    return;

  if (ieq(line, "PING") || ieq(line, "PONG") || ieq(line, "ID") || ieq(line, "STAT"))
  {
    replyPing();
    return;
  }
  if (ieq(line, "TEST"))
  {
    hidMove(120, 0, 0);
    gInjMoves++;
    replyPing();
    return;
  }
  if (ieq(line, "CLICK") || ieq(line, "LCLICK")) { Mouse.click(MOUSE_LEFT); return; }
  if (ieq(line, "RCLICK")) { Mouse.click(MOUSE_RIGHT); return; }
  if (ieq(line, "MCLICK")) { Mouse.click(MOUSE_MIDDLE); return; }
  if (ieq(line, "LDOWN")) { Mouse.press(MOUSE_LEFT); return; }
  if (ieq(line, "LUP")) { Mouse.release(MOUSE_LEFT); return; }
  if (ieq(line, "RDOWN")) { Mouse.press(MOUSE_RIGHT); return; }
  if (ieq(line, "RUP")) { Mouse.release(MOUSE_RIGHT); return; }

  char *comma = strchr(line, ',');
  if (!comma)
    return;
  *comma = 0;
  int dx = atoi(line);
  char *rest = comma + 1;
  char *c2 = strchr(rest, ',');
  int dy;
  int m = 0;
  if (c2)
  {
    *c2 = 0;
    dy = atoi(rest);
    m = atoi(c2 + 1);
  }
  else
    dy = atoi(rest);

  if (dx != 0 || dy != 0)
  {
    hidMove((int16_t)dx, (int16_t)dy, 0);
    gInjMoves++;
  }
  if (m == 1)
  {
    Mouse.press(MOUSE_LEFT);
    Mouse.release(MOUSE_LEFT);
    gButtons &= (uint8_t)~0x01;
  }
}

void pollSerial()
{
  char lastMove[64];
  lastMove[0] = 0;
  uint8_t n = 0;
  while (Serial.available() && n < 96)
  {
    n++;
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r')
    {
      if (gLineLen > 0)
      {
        gLine[gLineLen] = 0;
        if (strchr(gLine, ','))
        {
          strncpy(lastMove, gLine, sizeof(lastMove) - 1);
          lastMove[sizeof(lastMove) - 1] = 0;
        }
        else
          handleLine(gLine);
        gLineLen = 0;
      }
      gHaveBytes = false;
      continue;
    }
    if (gLineLen < sizeof(gLine) - 1)
      gLine[gLineLen++] = c;
    else
      gLineLen = 0;
    gLastRxMs = millis();
    gHaveBytes = true;
  }
  if (lastMove[0])
    handleLine(lastMove);
}

void setup()
{
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  Mouse.begin();
  Serial.begin(115200);
  delay(400);
  Serial.println(F("LEONARDO HS=0 BOOT"));
  HidBootMouse.SetReportParser(0, &gBootParser);

  if (Usb.Init() == -1)
  {
    gHostOk = false;
    Serial.println(F("LEONARDO HS=0 INIT_FAIL"));
  }
  else
  {
    gHostOk = true;
    delay(200);
    Serial.println(F("LEONARDO HS=1"));
  }
  digitalWrite(LED_BUILTIN, gHostOk ? HIGH : LOW);
}

void loop()
{
  if (gHostOk)
    Usb.Task();
  pollSerial();
  if (gHostOk)
    Usb.Task();

  static uint32_t lastBlink = 0;
  if (!gHostOk)
  {
    if (millis() - lastBlink > 120)
    {
      lastBlink = millis();
      digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));
    }
  }
  else if (gSawMouse)
    digitalWrite(LED_BUILTIN, HIGH);
  else if (millis() - lastBlink > 400)
  {
    lastBlink = millis();
    digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));
  }
}

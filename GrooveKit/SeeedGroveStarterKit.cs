using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.Adc;
using System;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Pwm;
using GHIElectronics.TinyCLR.Devices.I2c;
using GHIElectronics.TinyCLR.Pins;

namespace SeeedGroveStarterKit
{
    public class LightSensor
    {
        private AdcChannel Channel;
        public LightSensor(int AdcPinNumber)
        {
            Channel = AdcController.GetDefault().OpenChannel(AdcPinNumber);
        }
        // between 0 and 100
        public double ReadLightLevel()
        {
            return Channel.ReadRatio() * 100;
        }
    }

    public class TouchSensor
    {
        GpioPin Pin;
        public TouchSensor(int PinNumber)
        {
            Pin = GpioController.GetDefault().OpenPin(PinNumber);
            Pin.Write(GpioPinValue.Low);
            Pin.SetDriveMode(GpioPinDriveMode.Input);
            Pin.ValueChanged += Pin_ValueChanged;
        }

        public bool IsTouched()
        {
            return Pin.Read() == GpioPinValue.High;
        }


        /// <summary>
        /// The signature of button events.
        /// </summary>
        public delegate void TouchEventHandler();

        /// <summary>
        /// The event raised when a button is released.
        /// </summary>
        public event TouchEventHandler Touched;
        public event TouchEventHandler Untouched;
        private void Pin_ValueChanged(object sender, GpioPinValueChangedEventArgs e)
        {
            if (e.Edge == GpioPinEdge.RisingEdge)
                Touched?.Invoke();
            else
                Untouched?.Invoke();
        }

    }

    public class ServoMotor
    {
        private PwmPin servo;
        public ServoMotor(string controller, int PwmPinNumber)
        {
            PwmController PWM = PwmController.FromId(controller);

            servo = PWM.OpenPin(PwmPinNumber);
            PWM.SetDesiredFrequency(1 / 0.020);
        }
        public double MinPulseCalibration
        {
            set
            {
                if (value > 1.5 || value < 0.1)
                    throw new ArgumentOutOfRangeException("Must be between 0.1 and 1.5ms");
                _MinPulseCalibration = value;
            }
        }
        public double MaxPulseCalibration
        {
            set
            {
                if (value > 3 || value < 1.6)
                    throw new ArgumentOutOfRangeException("Must be between 1.6 and 3ms");
                _MaxPulseCalibration = value;
            }
        }
        // min and max pulse width in milliseconds
        private double _MinPulseCalibration = 1.0;
        private double _MaxPulseCalibration = 2.0;

        /// <summary>
        /// Sets the position of the Servo Motor.
        /// </summary>
        /// <param name="position">The position of the servo between 0 and 180 degrees.</param>
        public void SetPosition(double position)
        {
            if (position < 0 || position > 180) throw new ArgumentOutOfRangeException("degrees", "degrees must be between 0 and 180.");

            // Typically, with 50 hz, 0 degree is 0.05 and 180 degrees is 0.10
            //double duty = ((position / 180.0) * (0.10 - 0.05)) + 0.05;
            double duty = ((position / 180.0) * (_MaxPulseCalibration / 20 - _MinPulseCalibration / 20)) + _MinPulseCalibration / 20;


            servo.SetActiveDutyCyclePercentage(duty);
            servo.Start();
        }
    }

    public class Buzzer
    {
        private GpioPin Pin;
        public Buzzer(int GpioPinNumber)
        {
            Pin = GpioController.GetDefault().OpenPin(GpioPinNumber);
            Pin.Write(GpioPinValue.Low);
            Pin.SetDriveMode(GpioPinDriveMode.Output);
        }
        public void TurnOn()
        {
            Pin.Write(GpioPinValue.High);
        }
        public void TurnOff()
        {
            Pin.Write(GpioPinValue.Low);
        }
        public void Beep()
        {
            TurnOn();
            Thread.Sleep(10);
            TurnOff();
        }
    }

    public class LcdRgbBacklight
    {
        private I2cDevice DisplayDevice;
        private I2cDevice BacklightDevice;

        private byte _displayfunction;
        private byte _displaycontrol;
        private byte _displaymode;
        private byte _numlines, _currline;


        private byte LCD_CLEARDISPLAY = 0x01;
        private byte LCD_RETURNHOME = 0x02;
        private byte LCD_ENTRYMODESET = 0x04;
        private byte LCD_DISPLAYCONTROL = 0x08;
        private byte LCD_CURSORSHIFT = 0x10;
        private byte LCD_FUNCTIONSET = 0x20;
        private byte LCD_SETCGRAMADDR = 0x40;
        private byte LCD_SETDDRAMADDR = 0x80;

        private byte LCD_DISPLAYON = 0x04;
        private byte LCD_DISPLAYOFF = 0x00;
        private byte LCD_CURSORON = 0x02;
        private byte LCD_CURSOROFF = 0x00;
        private byte LCD_BLINKON = 0x01;
        private byte LCD_BLINKOFF = 0x00;
        private byte LCD_ENTRYRIGHT = 0x00;
        private byte LCD_ENTRYLEFT = 0x02;
        private byte LCD_ENTRYSHIFTINCREMENT = 0x01;
        private byte LCD_ENTRYSHIFTDECREMENT = 0x00;

        private byte REG_MODE1 = 0x00;
        private byte REG_MODE2 = 0x01;
        private byte REG_OUTPUT = 0x08;

        public LcdRgbBacklight()
        {
            var settings = new I2cConnectionSettings((0x7c >> 1));
            settings.SharingMode = I2cSharingMode.Shared;
            settings.BusSpeed = I2cBusSpeed.FastMode;

            //string aqs = I2cDevice.GetDeviceSelector("I2C1");
            DisplayDevice = I2cDevice.FromId(FEZ.I2cBus.I2c1, settings);

            settings = new I2cConnectionSettings((0xc4 >> 1));
            settings.SharingMode = I2cSharingMode.Shared;
            settings.BusSpeed = I2cBusSpeed.FastMode;

            BacklightDevice = I2cDevice.FromId(FEZ.I2cBus.I2c1, settings);

            ////////////////////////////////////
            // get the display going

            //byte cols = 6;
            byte lines = 2;
            byte dotsize = 0;


            if (lines > 1)
            {
                _displayfunction |= 0x08;// LCD_2LINE;
            }
            _numlines = lines;
            _currline = 0;

            // for some 1 line displays you can select a 10 pixel high font
            if ((dotsize != 0) && (lines == 1))
            {
                _displayfunction |= 0x04;// LCD_5x10DOTS;
            }

            // SEE PAGE 45/46 FOR INITIALIZATION SPECIFICATION!
            // according to datasheet, we need at least 40ms after power rises above 2.7V
            // before sending commands. Arduino can turn on way befer 4.5V so we'll wait 50
            //delayMicroseconds(50000);
            Thread.Sleep(50);

            // this is according to the hitachi HD44780 datasheet
            // page 45 figure 23

            // Send function set command sequence
            command((byte)(LCD_FUNCTIONSET | _displayfunction));
            //delayMicroseconds(4500);  // wait more than 4.1ms
            Thread.Sleep(5);

            // second try
            command((byte)(LCD_FUNCTIONSET | _displayfunction));
            //delayMicroseconds(150);
            Thread.Sleep(1);

            // third go
            command((byte)(LCD_FUNCTIONSET | _displayfunction));


            // finally, set # lines, font size, etc.
            command((byte)(LCD_FUNCTIONSET | _displayfunction));

            // turn the display on with no cursor or blinking default
            _displaycontrol = (byte)(LCD_DISPLAYON | LCD_CURSOROFF | LCD_BLINKOFF);
            EnableDisplay(true);

            // clear it off
            Clear();

            // Initialize to default text direction (for romance languages)
            _displaymode = (byte)(LCD_ENTRYLEFT | LCD_ENTRYSHIFTDECREMENT);
            // set the entry mode
            command((byte)(LCD_ENTRYMODESET | _displaymode));


            // backlight init
            WriteBacklightReg(REG_MODE1, 0);
            // set LEDs controllable by both PWM and GRPPWM registers
            WriteBacklightReg(REG_OUTPUT, 0xFF);
            // set MODE2 values
            // 0010 0000 -> 0x20  (DMBLNK to 1, ie blinky mode)
            WriteBacklightReg(REG_MODE2, 0x20);

            //setColorWhite();
            SetBacklightRGB(255, 0, 100);

        }

         // send command
        private void command(byte value)
        {
            byte[] dta = new byte[2] { 0x80, value };
            DisplayDevice.Write(dta);
            //i2c_send_byteS(dta, 2);
        }

        // send data
        private void write(byte value)
        {

            byte[] dta = new byte[2] { 0x40, value };
            //i2c_send_byteS(dta, 2);
            DisplayDevice.Write(dta);
            //return 1; // assume sucess
        }

        /********** high level commands, for the user! */

        public void Write(string s)
        {

            for (int i = 0; i < s.Length; i++)
                write((byte)s[i]);
        }
        public void Clear()
        {
            command(LCD_CLEARDISPLAY);        // clear display, set cursor position to zero
            //delayMicroseconds(2000);          // this command takes a long time!
            Thread.Sleep(2);
        }

        public void GoHome()
        {
            command(LCD_RETURNHOME);        // set cursor position to zero
            //delayMicroseconds(2000);        // this command takes a long time!
            Thread.Sleep(2);
        }

        public void SetCursor(byte col, byte row)
        {

            col = (byte)(row == 0 ? (col | 0x80) : (col | 0xc0));
            command(col);
            //byte[] dta = new byte[2] { 0x80, col };
            //DisplayDevice.Write(dta);

            //i2c_send_byteS(dta, 2);

        }

        // Turn the display on/off (quickly)
        public void EnableDisplay(bool on)
        {
            if (on)
                _displaycontrol |= LCD_DISPLAYON;
            else
                _displaycontrol &= (byte)~LCD_DISPLAYON;

            command((byte)(LCD_DISPLAYCONTROL | _displaycontrol));
        }

        // =============================================================================

        // Control the backlight LED blinking
        private void WriteBacklightReg(byte addr, byte data)
        {
            byte[] wb = new byte[2];
            wb[0] = addr;
            wb[1] = data;

            BacklightDevice.Write(wb);

            //I2C.WriteRead((0xc4 >> 1), wb, 0, 2, rb, 0, 0, out written, out read);

        }

        public void BlinkBacklight(bool on)
        {
            // blink period in seconds = (<reg 7> + 1) / 24
            // on/off ratio = <reg 6> / 256
            if (on)
            {
                WriteBacklightReg(0x07, 0x17);  // blink every second
                WriteBacklightReg(0x06, 0x7f);  // half on, half off
            }
            else
            {
                WriteBacklightReg(0x07, 0x00);
                WriteBacklightReg(0x06, 0xff);
            }
        }
        public void SetBacklightRGB(byte r, byte g, byte b)
        {

            byte REG_RED = 0x04;    // pwm2
            byte REG_GREEN = 0x03;      // pwm1
            byte REG_BLUE = 0x02;      // pwm0

            WriteBacklightReg(REG_RED, r);
            WriteBacklightReg(REG_GREEN, g);
            WriteBacklightReg(REG_BLUE, b);
        }
    }
}
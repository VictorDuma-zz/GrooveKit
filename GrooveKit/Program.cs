using System;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Networking.SPWF04Sx;
using GHIElectronics.TinyCLR.Devices.Spi;
using System.Text;
using System.Diagnostics;
using SeeedGroveStarterKit;

namespace GrooveKit {
    class Program {
        private static GpioPin garageLed;
        private static SPWF04SxInterface wifi;
        private static bool connected;
        private static bool isDoorOpened;
        private static bool socketOpened;
        private static int id;
        public static string host;
        public static int port;

        static void Main() {
            TouchSensor touch = new TouchSensor(FEZ.GpioPin.D7);
            LightSensor light = new LightSensor(FEZ.AdcChannel.A3);
            ServoMotor servo = new ServoMotor(FEZ.PwmPin.Controller4.Id, FEZ.PwmPin.Controller4.D5);
            Buzzer buzz = new Buzzer(FEZ.GpioPin.D4);
            LcdRgbBacklight lcd = new LcdRgbBacklight();

            host = "192.168.1.152";
            port = 80;

            var buffer = new byte[512];
            var cont = GpioController.GetDefault();
            var reset = cont.OpenPin(FEZ.GpioPin.WiFiReset);
            var irq = cont.OpenPin(FEZ.GpioPin.WiFiInterrupt);
            var spi = SpiDevice.FromId(FEZ.SpiBus.WiFi, SPWF04SxInterface.GetConnectionSettings(FEZ.GpioPin.WiFiChipSelect));

            connected = false;
            socketOpened = false;
            garageLed = cont.OpenPin(FEZ.GpioPin.D2);
            servo.SetPosition(180);
            garageLed.SetDriveMode(GpioPinDriveMode.Output);

            wifi = new SPWF04SxInterface(spi, irq, reset);

            wifi.IndicationReceived += (s, e) => Debug.WriteLine($"WIND: {Program.WindToName(e.Indication)} {e.Message}");
            wifi.ErrorReceived += (s, e) => Debug.WriteLine($"ERROR: {e.Error} {e.Message}");

            wifi.TurnOn();
            //wifi.JoinNetwork("GHI", "ghi555wifi.");

            lcd.Clear();
            lcd.SetBacklightRGB(100, 100, 100);
            lcd.Write("Time:");

            while (!connected) {
                ListenWind();
                Thread.Sleep(200);
            }

            StringBuilder builder = new StringBuilder();

            while (connected) {
                if (!socketOpened) {
                    id = wifi.OpenSocket(host, port, SPWF04SxConnectionyType.Tcp, SPWF04SxConnectionSecurityType.None);
                    socketOpened = true;
                }

                var hour = DateTime.UtcNow.Hour;
                var minute = DateTime.UtcNow.Minute;
                var second = DateTime.UtcNow.Second;
                lcd.SetCursor(7, 1);
                lcd.Write($"{hour}:{minute}:{second}");

                if (touch.IsTouched())
                    wifi.WriteSocket(id, Encoding.UTF8.GetBytes("Someone wants to open the garage"));

                if (light.ReadLightLevel() > 60 && isDoorOpened == true) {
                    //Debug.WriteLine(light.ReadLightLevel().ToString());
                    wifi.WriteSocket(id, Encoding.UTF8.GetBytes("Car in the garage"));
                    while (light.ReadLightLevel() > 60)
                        Thread.Sleep(50);
                    wifi.WriteSocket(id, Encoding.UTF8.GetBytes("You can close the garage"));
                }

                if (wifi.QuerySocket(id) is var avail && avail > 0) {
                    wifi.ReadSocket(id, buffer, 0, Math.Min(avail, buffer.Length));

                    for (var k = 0; k < buffer.Length; k++) {
                        if (buffer[k] != 0) {
                            char result = (char)buffer[k];
                            builder.Append(result);
                            buffer[k] = 0;
                        }
                    }
                    Debug.WriteLine(builder.ToString());
                }
                string command = builder.ToString();
                builder.Clear();

                switch (command) {
                    case "open":
                        buzz.Beep();
                        servo.SetPosition(0);
                        garageLed.Write(GpioPinValue.High);
                        isDoorOpened = true;
                        break;
                    case "close":
                        buzz.Beep();
                        servo.SetPosition(180);
                        garageLed.Write(GpioPinValue.Low);
                        break;
                    default:
                        break;
                }
                Thread.Sleep(100);
            }
        }

        public static void ListenWind() {
            wifi.IndicationReceived += WaitForEvents;
        }

        public static void WaitForEvents(object sender, SPWF04SxIndicationReceivedEventArgs e) {
            if (e.Indication == SPWF04SxIndication.NtpServerDelivery && connected == false) {
                connected = true;
                Debug.WriteLine("Connected");
            }
            if (e.Indication == SPWF04SxIndication.SocketClosed) {
                socketOpened = false;
                Debug.WriteLine("Connection lost");
            }
        }

        private static string WindToName(SPWF04SxIndication wind) {
            switch (wind) {
                case SPWF04SxIndication.ConsoleActive: return nameof(SPWF04SxIndication.ConsoleActive);
                case SPWF04SxIndication.PowerOn: return nameof(SPWF04SxIndication.PowerOn);
                case SPWF04SxIndication.Reset: return nameof(SPWF04SxIndication.Reset);
                case SPWF04SxIndication.WatchdogRunning: return nameof(SPWF04SxIndication.WatchdogRunning);
                case SPWF04SxIndication.LowMemory: return nameof(SPWF04SxIndication.LowMemory);
                case SPWF04SxIndication.WiFiHardwareFailure: return nameof(SPWF04SxIndication.WiFiHardwareFailure);
                case SPWF04SxIndication.ConfigurationFailure: return nameof(SPWF04SxIndication.ConfigurationFailure);
                case SPWF04SxIndication.HardFault: return nameof(SPWF04SxIndication.HardFault);
                case SPWF04SxIndication.StackOverflow: return nameof(SPWF04SxIndication.StackOverflow);
                case SPWF04SxIndication.MallocFailed: return nameof(SPWF04SxIndication.MallocFailed);
                case SPWF04SxIndication.RadioStartup: return nameof(SPWF04SxIndication.RadioStartup);
                case SPWF04SxIndication.WiFiPSMode: return nameof(SPWF04SxIndication.WiFiPSMode);
                case SPWF04SxIndication.Copyright: return nameof(SPWF04SxIndication.Copyright);
                case SPWF04SxIndication.WiFiBssRegained: return nameof(SPWF04SxIndication.WiFiBssRegained);
                case SPWF04SxIndication.WiFiSignalLow: return nameof(SPWF04SxIndication.WiFiSignalLow);
                case SPWF04SxIndication.WiFiSignalOk: return nameof(SPWF04SxIndication.WiFiSignalOk);
                case SPWF04SxIndication.BootMessages: return nameof(SPWF04SxIndication.BootMessages);
                case SPWF04SxIndication.KeytypeNotImplemented: return nameof(SPWF04SxIndication.KeytypeNotImplemented);
                case SPWF04SxIndication.WiFiJoin: return nameof(SPWF04SxIndication.WiFiJoin);
                case SPWF04SxIndication.WiFiJoinFailed: return nameof(SPWF04SxIndication.WiFiJoinFailed);
                case SPWF04SxIndication.WiFiScanning: return nameof(SPWF04SxIndication.WiFiScanning);
                case SPWF04SxIndication.ScanBlewUp: return nameof(SPWF04SxIndication.ScanBlewUp);
                case SPWF04SxIndication.ScanFailed: return nameof(SPWF04SxIndication.ScanFailed);
                case SPWF04SxIndication.WiFiUp: return nameof(SPWF04SxIndication.WiFiUp);
                case SPWF04SxIndication.WiFiAssociationSuccessful: return nameof(SPWF04SxIndication.WiFiAssociationSuccessful);
                case SPWF04SxIndication.StartedAP: return nameof(SPWF04SxIndication.StartedAP);
                case SPWF04SxIndication.APStartFailed: return nameof(SPWF04SxIndication.APStartFailed);
                case SPWF04SxIndication.StationAssociated: return nameof(SPWF04SxIndication.StationAssociated);
                case SPWF04SxIndication.DhcpReply: return nameof(SPWF04SxIndication.DhcpReply);
                case SPWF04SxIndication.WiFiBssLost: return nameof(SPWF04SxIndication.WiFiBssLost);
                case SPWF04SxIndication.WiFiException: return nameof(SPWF04SxIndication.WiFiException);
                case SPWF04SxIndication.WiFiHardwareStarted: return nameof(SPWF04SxIndication.WiFiHardwareStarted);
                case SPWF04SxIndication.WiFiNetwork: return nameof(SPWF04SxIndication.WiFiNetwork);
                case SPWF04SxIndication.WiFiUnhandledEvent: return nameof(SPWF04SxIndication.WiFiUnhandledEvent);
                case SPWF04SxIndication.WiFiScan: return nameof(SPWF04SxIndication.WiFiScan);
                case SPWF04SxIndication.WiFiUnhandledIndication: return nameof(SPWF04SxIndication.WiFiUnhandledIndication);
                case SPWF04SxIndication.WiFiPoweredDown: return nameof(SPWF04SxIndication.WiFiPoweredDown);
                case SPWF04SxIndication.HWInMiniAPMode: return nameof(SPWF04SxIndication.HWInMiniAPMode);
                case SPWF04SxIndication.WiFiDeauthentication: return nameof(SPWF04SxIndication.WiFiDeauthentication);
                case SPWF04SxIndication.WiFiDisassociation: return nameof(SPWF04SxIndication.WiFiDisassociation);
                case SPWF04SxIndication.WiFiUnhandledManagement: return nameof(SPWF04SxIndication.WiFiUnhandledManagement);
                case SPWF04SxIndication.WiFiUnhandledData: return nameof(SPWF04SxIndication.WiFiUnhandledData);
                case SPWF04SxIndication.WiFiUnknownFrame: return nameof(SPWF04SxIndication.WiFiUnknownFrame);
                case SPWF04SxIndication.Dot11Illegal: return nameof(SPWF04SxIndication.Dot11Illegal);
                case SPWF04SxIndication.WpaCrunchingPsk: return nameof(SPWF04SxIndication.WpaCrunchingPsk);
                case SPWF04SxIndication.WpaTerminated: return nameof(SPWF04SxIndication.WpaTerminated);
                case SPWF04SxIndication.WpaStartFailed: return nameof(SPWF04SxIndication.WpaStartFailed);
                case SPWF04SxIndication.WpaHandshakeComplete: return nameof(SPWF04SxIndication.WpaHandshakeComplete);
                case SPWF04SxIndication.GpioInterrupt: return nameof(SPWF04SxIndication.GpioInterrupt);
                case SPWF04SxIndication.Wakeup: return nameof(SPWF04SxIndication.Wakeup);
                case SPWF04SxIndication.PendingData: return nameof(SPWF04SxIndication.PendingData);
                case SPWF04SxIndication.InputToRemote: return nameof(SPWF04SxIndication.InputToRemote);
                case SPWF04SxIndication.OutputFromRemote: return nameof(SPWF04SxIndication.OutputFromRemote);
                case SPWF04SxIndication.SocketClosed: return nameof(SPWF04SxIndication.SocketClosed);
                case SPWF04SxIndication.IncomingSocketClient: return nameof(SPWF04SxIndication.IncomingSocketClient);
                case SPWF04SxIndication.SocketClientGone: return nameof(SPWF04SxIndication.SocketClientGone);
                case SPWF04SxIndication.SocketDroppingData: return nameof(SPWF04SxIndication.SocketDroppingData);
                case SPWF04SxIndication.RemoteConfiguration: return nameof(SPWF04SxIndication.RemoteConfiguration);
                case SPWF04SxIndication.FactoryReset: return nameof(SPWF04SxIndication.FactoryReset);
                case SPWF04SxIndication.LowPowerMode: return nameof(SPWF04SxIndication.LowPowerMode);
                case SPWF04SxIndication.GoingIntoStandby: return nameof(SPWF04SxIndication.GoingIntoStandby);
                case SPWF04SxIndication.ResumingFromStandby: return nameof(SPWF04SxIndication.ResumingFromStandby);
                case SPWF04SxIndication.GoingIntoDeepSleep: return nameof(SPWF04SxIndication.GoingIntoDeepSleep);
                case SPWF04SxIndication.ResumingFromDeepSleep: return nameof(SPWF04SxIndication.ResumingFromDeepSleep);
                case SPWF04SxIndication.StationDisassociated: return nameof(SPWF04SxIndication.StationDisassociated);
                case SPWF04SxIndication.SystemConfigurationUpdated: return nameof(SPWF04SxIndication.SystemConfigurationUpdated);
                case SPWF04SxIndication.RejectedFoundNetwork: return nameof(SPWF04SxIndication.RejectedFoundNetwork);
                case SPWF04SxIndication.RejectedAssociation: return nameof(SPWF04SxIndication.RejectedAssociation);
                case SPWF04SxIndication.WiFiAuthenticationTimedOut: return nameof(SPWF04SxIndication.WiFiAuthenticationTimedOut);
                case SPWF04SxIndication.WiFiAssociationTimedOut: return nameof(SPWF04SxIndication.WiFiAssociationTimedOut);
                case SPWF04SxIndication.MicFailure: return nameof(SPWF04SxIndication.MicFailure);
                case SPWF04SxIndication.UdpBroadcast: return nameof(SPWF04SxIndication.UdpBroadcast);
                case SPWF04SxIndication.WpsGeneratedDhKeyset: return nameof(SPWF04SxIndication.WpsGeneratedDhKeyset);
                case SPWF04SxIndication.WpsEnrollmentAttemptTimedOut: return nameof(SPWF04SxIndication.WpsEnrollmentAttemptTimedOut);
                case SPWF04SxIndication.SockdDroppingClient: return nameof(SPWF04SxIndication.SockdDroppingClient);
                case SPWF04SxIndication.NtpServerDelivery: return nameof(SPWF04SxIndication.NtpServerDelivery);
                case SPWF04SxIndication.DhcpFailedToGetLease: return nameof(SPWF04SxIndication.DhcpFailedToGetLease);
                case SPWF04SxIndication.MqttPublished: return nameof(SPWF04SxIndication.MqttPublished);
                case SPWF04SxIndication.MqttClosed: return nameof(SPWF04SxIndication.MqttClosed);
                case SPWF04SxIndication.WebSocketData: return nameof(SPWF04SxIndication.WebSocketData);
                case SPWF04SxIndication.WebSocketClosed: return nameof(SPWF04SxIndication.WebSocketClosed);
                case SPWF04SxIndication.FileReceived: return nameof(SPWF04SxIndication.FileReceived);
                default: return "Other";
            }
        }
    }
}

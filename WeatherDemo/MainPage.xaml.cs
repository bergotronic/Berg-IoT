// Copyright (c) Microsoft. All rights reserved.

using System;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Devices.I2c;
using Windows.Devices.Enumeration;
using Windows.System.Threading;
using Windows.UI.Xaml.Media.Imaging;
using System.Xml.Serialization;
using System.Xml;
using System.Runtime.Serialization.Json;
using System.IO;
using Windows.Web.Http.Headers;
using Windows.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using System.Diagnostics;

namespace WeatherDemo
{
    public sealed partial class MainPage : Page
    {
        //WEATHER
        BackgroundTaskDeferral deferral;
        I2cDevice sensor;
        DispatcherTimer timer;
        private ThreadPoolTimer TPtimer;
        private ThreadPoolTimer Audiotimer;

        public string TimeStamp { get; set; }
        public float Altitude { get; set; }
        public float CelsiusTemperature { get; set; }
        public float FahrenheitTemperature { get; set; }
        public float Humidity { get; set; }
        public float BarometricPressure { get; set; }

        //SOUND

        public int soundcount = 0;
        private const int SDA_PIN = 2;
        private const int SCL_PIN = 3;
        private const int BUTTON_PIN = 13;
        private const int SOUND_PIN = 5;

        private const int ENVTime = 1000;
        private const int SOUNDTime = 2500;
        private const string DeviceConnectionString = "Your Device String Here - Hint - Use windows iot device explorer";

        private String AzureTransmit;

        private BitmapImage AzureIconOff = new BitmapImage(new Uri("ms-appx:///Assets/Stream AnalyticsDESAT.png"));
        private BitmapImage AzureIconON = new BitmapImage(new Uri("ms-appx:///Assets/Stream Analytics.png"));



        private GpioPin buttonPin;
        private GpioPin ledPin;
        private GpioPin soundPin;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        public BitmapImage Cricket;
        public BitmapImage Clap;

        public string ReadInProgress;
        public string DeviceLocation;

        public string internetConnected;

        public string log;
        public string pinvalue;

        public string eventtext;
        public string AzureMode;
        public string AzureModeStatus;

        public string GPIOStatus;

        private const int STATUS_LED_BLUE_PIN = 6;
        public GpioPin BlueLEDPin { get; private set; }

        class MySensorData
        {
            public String Name { get; set; }
            public String SensorType { get; set; }
            public String TimeStamp { get; set; }
            public String DataValue { get; set; }
            public String UnitOfMeasure { get; set; }
            public String Location { get; set; }
            public String DataType { get; set; }
            public String MeasurementID { get; set; }

        }



        public MainPage()
        {

            Debug.WriteLine(DateTime.Now + " Main Page Initialization Starting");

            Cricket = new BitmapImage(new Uri("ms-appx:///Assets/whiteninja.png"));
            Clap = new BitmapImage(new Uri("ms-appx:///Assets/clap.png"));
            ReadInProgress = "False";
            internetConnected = "True";
            AzureMode = "NOTransmit";


            Debug.WriteLine(DateTime.Now + " Component Initialization");

            InitializeComponent();

            Debug.WriteLine(DateTime.Now + " Checking for Internet Connection");
            CheckForInternetConnection();

          //  Debug.WriteLine(DateTime.Now + " Checking Location");
          //  InitLocation();

            Debug.WriteLine(DateTime.Now + " Init GPIO");
            InitGPIO();

            Debug.WriteLine(DateTime.Now + " Init SPI");
            InitSPI();

            Debug.WriteLine(DateTime.Now + " Init Audio Listener");
            InitAudioListen();

            Debug.WriteLine(DateTime.Now + " Initialization Complete");

            Log_Event("Device Initilization Done","BergIOTDemo","DeviceInit","Status","N/A");


        





    }

        private async void InitLocation()
        {
            //GetLocation
            var httpClientLoc = new HttpClient();
            var locationuri = new Uri("http://api.hostip.info/get_json.php");

            HttpRequestMessage msgLoc = new HttpRequestMessage(new HttpMethod("GET"), new Uri(locationuri.ToString()));
            HttpResponseMessage response = await httpClientLoc.GetAsync(locationuri).AsTask();

            string responseBodyAsText;
            responseBodyAsText = await response.Content.ReadAsStringAsync();

            //{ "country_name":"UNITED STATES","country_code":"US","city":"Oregon, WI","ip":"24.183.53.104"}

            dynamic JSONContent = JObject.Parse(responseBodyAsText);

            string country_name = JSONContent.country_name;
            string country_code = JSONContent.country_code;
            string city = JSONContent.city;
            string ip = JSONContent.ip;

            string locationstr = (city + "," + country_name);
            System.Diagnostics.Debug.WriteLine("Device Location: " + city + "," + country_name);

            DeviceLocation = locationstr;


        }

        public async void CheckForInternetConnection()
        {

            try
            {
                //GetLocation
                var httpClientLoc = new HttpClient();
                var locationuri = new Uri("http://www.google.com");

                HttpRequestMessage msgLoc = new HttpRequestMessage(new HttpMethod("GET"), new Uri(locationuri.ToString()));
                HttpResponseMessage response = await httpClientLoc.GetAsync(locationuri).AsTask();

                string responseBodyAsText;
                responseBodyAsText = await response.Content.ReadAsStringAsync();

                internetConnected = "True";
                // AzureMode = "Transmit";
                System.Diagnostics.Debug.WriteLine(responseBodyAsText.ToString());

                imageStreamLogo.Visibility = Visibility.Visible;
                LabelAzureStatus.Text = "Connected!";


            }
            catch
            {
                internetConnected = "False";
                LabelAzureStatus.Text = "No Connection";
                System.Diagnostics.Debug.WriteLine("No Internet");
                // imageStreamLogo.Visibility = Visibility.Collapsed;

            }
        } 


        public async void InitSPI()
        {
            
            String aqs = I2cDevice.GetDeviceSelector("I2C1");
            var deviceInfo = await DeviceInformation.FindAllAsync(aqs);
            sensor = await I2cDevice.FromIdAsync(deviceInfo[0].Id, new I2cConnectionSettings(0x40));
            TPtimer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(ENVTime)); // .FromMilliseconds(1000));
        }

        public async void InitAudioListen()
        {
            Audiotimer = ThreadPoolTimer.CreatePeriodicTimer(AudioTimer_Tick, TimeSpan.FromMilliseconds(SOUNDTime)); // .FromMilliseconds(1000));
        }

        private async void AudioTimer_Tick(ThreadPoolTimer timer)
        {

           // System.Diagnostics.Debug.WriteLine(soundPin.Read().ToString());
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {

                if (GPIOStatus == "Connected")
                {
                    if (soundPin.Read().ToString() == "Low")
                    {
                        soundcount = 0;
                        ledEllipse.Fill = new SolidColorBrush(Windows.UI.Colors.Gray);
                        await Log_Event("0", "BergIOTDemo", "Audio", "NoisePresence", "bol");
                        ImgSoundType.Source = Cricket;
                    }

                    else
                    {
                        soundcount = soundcount + 1;
                        if (soundcount >= 5)
                        {
                            ledEllipse.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
                            await Log_Event("100", "BergIOTDemo", "Audio", "NoisePresence", "bol");
                            ImgSoundType.Source = Clap;
                        }

                    }
                }
               


            });



        }

        private async Task Log_Event(string DataValue, string Name, string Sensor, string DataType, string UnitOfMeasure)
        {
            //Debug
            DateTime localDate = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("Event: " + Name + "-" + Sensor + "-" + DataValue + "-" + localDate.ToString());

            //to Azure
            if (AzureMode == "Transmit")
                if (internetConnected == "True")
                {
                    {
                        
                        //Init httpClinet:
                        //var httpClient = new HttpClient();

                        System.Diagnostics.Debug.WriteLine("Starting Azure Transmit");


                        MySensorData SensorInstance = new MySensorData();
                        SensorInstance.Name = Name;
                        SensorInstance.SensorType = Sensor;
                        SensorInstance.TimeStamp = DateTime.Now.ToString();
                        SensorInstance.DataValue = DataValue;
                        SensorInstance.DataType = DataType;
                        SensorInstance.UnitOfMeasure = UnitOfMeasure;
                        SensorInstance.MeasurementID = Guid.NewGuid().ToString();
                        SensorInstance.Location = DeviceLocation;


                       string jsoncontent = JsonConvert.SerializeObject(SensorInstance);



                        DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString);

                        string dataBuffer;
                        dataBuffer = jsoncontent;

                        System.Diagnostics.Debug.WriteLine(jsoncontent);


                        Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                        await deviceClient.SendEventAsync(eventMessage);

                        
                        System.Diagnostics.Debug.WriteLine("Azure Transmit Done");



                    }
                }


        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {

            if (GPIOStatus == "Connected")
            {

             //   CheckForInternetConnection();

                var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                {
                    if (ReadInProgress == "False")
                    {
                        ReadInProgress = "True";

                        textBlockTime.Text = (DateTime.Now).ToString();

                    //Read temperature and humidity and send result to debugger output window
                    //System.Diagnostics.Debug.WriteLine("Weather Tick");
                    BlueLEDPin.Write(Windows.Devices.Gpio.GpioPinValue.High);


                        byte[] tempCommand = new byte[1] { 0xE3 };
                        byte[] tempData = new byte[2];
                        sensor.WriteRead(tempCommand, tempData);
                        var rawTempReading = tempData[0] << 8 | tempData[1];
                        var tempRatio = rawTempReading / (float)65536;
                        double temperature = (-46.85 + (175.72 * tempRatio)) * 9 / 5 + 32;

                    //[°C] = ([°F] - 32) × 5/9
                    //[°F] = [°C] × 9/5 + 32

                    string strtemperature = (temperature.ToString()).Substring(0, 4);
                        LabelTemp.Text = ("Temperature: " + strtemperature + " F");
                        await Log_Event(strtemperature, "BergIOTDemo", "Environmental", "Temperature", "F");


                        byte[] humidityCommand = new byte[1] { 0xE5 };
                        byte[] humidityData = new byte[2];
                        sensor.WriteRead(humidityCommand, humidityData);
                        var rawHumidityReading = humidityData[0] << 8 | humidityData[1];
                        var humidityRatio = rawHumidityReading / (float)65536;
                        double humidity = -6 + (125 * humidityRatio);
                        string strhumidity = (humidity.ToString()).Substring(0, 4);
                        LabelHum.Text = ("Humidity: " + strhumidity + " %");
                        await Log_Event(strhumidity, "BergIOTDemo", "Environmental", "Humidity", "%");

                        BlueLEDPin.Write(Windows.Devices.Gpio.GpioPinValue.Low);
                    }


                    ReadInProgress = "False";
                });
            }

        }

        private async void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                // GpioStatus.Text = "There is no GPIO controller on this device.";
               
                return;
            }

            else
            {

                GPIOStatus = "Connected";
                soundPin = gpio.OpenPin(SOUND_PIN);

                BlueLEDPin = gpio.OpenPin(STATUS_LED_BLUE_PIN);
                BlueLEDPin.Write(GpioPinValue.Low);
                BlueLEDPin.SetDriveMode(GpioPinDriveMode.Output);

                soundPin.SetDriveMode(GpioPinDriveMode.Input);
                soundPin.DebounceTimeout = TimeSpan.FromMilliseconds(100);
                // soundPin.ValueChanged += soundPin_ValueChanged;

                await Log_Event("GPIO pins initialized correctly.", "BergIOTDemo", "DeviceInit", "Status", "N/A");

            }




        }


        private void soundPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // toggle the state of the sound setting whenever gate is on
            if (GPIOStatus == "Connected")
            {

                // need to invoke UI updates on the UI thread because this event
                // handler gets invoked on a separate thread.
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {



                    if (e.Edge == GpioPinEdge.FallingEdge)
                    {
                        ledEllipse.Fill = new SolidColorBrush(Windows.UI.Colors.Gray);
                        await Log_Event("0", "BergIOTDemo", "Audio", "NoisePresence", "bol");
                        ImgSoundType.Source = Cricket;
                    }

                    else
                    {
                        ledEllipse.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
                        await Log_Event("100", "BergIOTDemo", "Audio", "NoisePresence", "bol");
                        ImgSoundType.Source = Clap;
                    }


                });
            }

        }

        private void imageStreamLogo_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {


            Debug.WriteLine(DateTime.Now + " AzureAutomation Button Pressed!");


            if (AzureMode == "NOTrasmit")
            {
                AzureMode = "Transmit";
                imageStreamLogo.Source = AzureIconON;
                Debug.WriteLine(DateTime.Now + " Azure Transmit Now Enabled");
            }

            else
            {

                AzureMode = "NOTrasmit";
                imageStreamLogo.Source = AzureIconOff;
                Debug.WriteLine(DateTime.Now + " Azure Transmit Now DISABLED");
            }



        }
    }
    

}



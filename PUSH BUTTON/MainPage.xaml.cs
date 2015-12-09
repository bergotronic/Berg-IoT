// Copyright (c) Microsoft. All rights reserved.

using System;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using System.Net;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.UI.Xaml.Media.Imaging;

namespace PushButton
{
    public sealed partial class MainPage : Page
    {

      
        private const int LED_PIN = 6;
        private const int BUTTON_PIN = 5;
        private GpioPin ledPin;
        private GpioPin buttonPin;
        private GpioPinValue ledPinValue = GpioPinValue.High;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        private static HttpResponseMessage response;

        private String AzureTransmit;
                                                                     
        private BitmapImage AzureIconOff = new BitmapImage(new Uri("ms-appx:///Assets/Azure automationDESAT.png"));
        private BitmapImage AzureIconON = new BitmapImage(new Uri("ms-appx:///Assets/Azure automation.png"));


        public MainPage()
        {
            AzureTransmit = "False";
            InitializeComponent();
            InitGPIO();
        }

        private void InitGPIO()
        {


            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            buttonPin = gpio.OpenPin(BUTTON_PIN);
            ledPin = gpio.OpenPin(LED_PIN);

            // Initialize LED to the OFF state by first writing a HIGH value
            // We write HIGH because the LED is wired in a active LOW configuration
            ledPin.Write(GpioPinValue.High); 
            ledPin.SetDriveMode(GpioPinDriveMode.Output);

            // Check if input pull-up resistors are supported
            if (buttonPin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                buttonPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                buttonPin.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            buttonPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            buttonPin.ValueChanged += buttonPin_ValueChanged;

            GpioStatus.Text = "GPIO pins initialized correctly.";
        }




        private void buttonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
           

            // toggle the state of the LED every time the button is pressed
            if (e.Edge == GpioPinEdge.FallingEdge)
            {
                ledPinValue = (ledPinValue == GpioPinValue.Low) ?
                GpioPinValue.High : GpioPinValue.Low;
                ledPin.Write(ledPinValue);
            }

            // need to invoke UI updates on the UI thread because this event
            // handler gets invoked on a separate thread.
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {


                //  
            


                if (e.Edge == GpioPinEdge.FallingEdge)
                {

                    ledEllipse.Fill = (ledPinValue == GpioPinValue.Low) ? 
                    redBrush : grayBrush;
                    GpioStatus.Text = "Button Pressed";
                    Debug.WriteLine(DateTime.Now + " Someone Pressed the Button!");

                    if (AzureTransmit == "True")
                    {
                        Debug.WriteLine(DateTime.Now + " Do Webhook!");
                        WebHook();
                    }



                }
                else
                {
                    GpioStatus.Text = "Button Released";
                }
            });
        }


        private async void WebHook()
        {
            Debug.WriteLine(DateTime.Now + " Calling Azure Automation WebHook!");

            using (var client = new HttpClient())
            {

                client.BaseAddress = new Uri("https://s1events.azure-automation.net/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // HTTP POST
                // response = await client.PostAsJsonAsync("https://s1events.azure-automation.net/webhooks?token=NuX9bfyiWzKM45UAoXp1WQ7mW2PPoDkaMUtxQlL%2f7ps%3d", "");
                //https://s1events.azure-automation.net/webhooks?token=pORbQ527NLVkze%2bf2Kyt%2bNI68%2bz8aiELViZyWp63Cec%3d

                response = await client.PostAsJsonAsync("webhooks?token=YOUR TOKEN HERE", "");
                if (response.IsSuccessStatusCode)
                {

                    // Debug.WriteLine(response.Content.ToString());
                    Debug.WriteLine(response.StatusCode.ToString());
                }

            }

            Debug.WriteLine(DateTime.Now + " WebHook Function Complete!");
        }






        private void image2_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now + " AzureAutomation Button Pressed!");
                     

            if(AzureTransmit == "False")
            {
                AzureTransmit = "True";
                imageAzureAutomation.Source = AzureIconON;
                Debug.WriteLine(DateTime.Now + " Azure Transmit Now Enabled");
            }

            else
            {
                
                AzureTransmit = "False";
                imageAzureAutomation.Source = AzureIconOff;
                Debug.WriteLine(DateTime.Now + " Azure Transmit Now DISABLED");
            }



        }
    }
}

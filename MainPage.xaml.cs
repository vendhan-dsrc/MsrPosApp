using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.PointOfService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MsrPosApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MagneticStripeReader _reader;
        private ClaimedMagneticStripeReader _claimedReader;

        public MainPage() {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e) {
            StatusText.Text = "Initializing MSR...";

            // 1. Get the default reader connected to the system
            _reader = await MagneticStripeReader.GetDefaultAsync();

            if (_reader != null) {
                StatusText.Text = $"MSR Found: {_reader.DeviceId}\n";

                // 2. Read Supported Card Types
                LogSupportedCardTypes();

                // 3. Claim the reader for exclusive access
                _claimedReader = await _reader.ClaimReaderAsync();

                if (_claimedReader != null) {
                    // 4. Hook up listeners for data events
                    // VendorSpecificDataReceived catches raw swipes like gift cards
                    _claimedReader.VendorSpecificDataReceived += ClaimedReader_VendorSpecificDataReceived;
                    _claimedReader.BankCardDataReceived += ClaimedReader_BankCardDataReceived;

                    // You must enable data transmission on the claimed reader
                    await _claimedReader.EnableAsync();
                    StatusText.Text += "Reader Claimed and Enabled. Ready to swipe!\n";
                } else {
                    StatusText.Text += "Failed to claim the reader (maybe another app is using it).\n";
                }
            } else {
                StatusText.Text = "No MSR detected.";
            }
        }

        private void LogSupportedCardTypes() {
            StatusText.Text += "--- Supported Card Types ---\n";

            // SupportedCardTypes is actually a list/collection of uints
            var typesList = _reader.SupportedCardTypes;

            if (typesList != null && typesList.Length > 0) {
                foreach (uint typeValue in typesList) {
                    // Cast the uint back to the readable Enum name
                    string cardTypeName = Enum.GetName(typeof(MagneticStripeReaderCardTypes), typeValue);

                    StatusText.Text += $"- {cardTypeName ?? "Unknown"} (Raw Value: {typeValue})\n";
                }
            } else {
                StatusText.Text += "No specific card types listed by the device driver.\n";
            }

            StatusText.Text += "----------------------------\n";
        }

        // Triggers when a custom/gift card is swiped
        private void ClaimedReader_VendorSpecificDataReceived(ClaimedMagneticStripeReader sender, MagneticStripeReaderVendorSpecificCardDataReceivedEventArgs args) {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                SwipeOutput.Text = "=== GIFT CARD / VENDOR SWIPE DETECTED ===\n";

                if (args.Report != null) {
                    // Extract Track 1 Data
                    if (args.Report.Track1 != null && args.Report.Track1.Data != null) {
                        byte[] track1Bytes = args.Report.Track1.Data.ToArray(); // Converts IBuffer to byte[]
                        SwipeOutput.Text += $"Track 1 Raw: {Encoding.UTF8.GetString(track1Bytes)}\n";
                    }

                    // Extract Track 2 Data
                    if (args.Report.Track2 != null && args.Report.Track2.Data != null) {
                        byte[] track2Bytes = args.Report.Track2.Data.ToArray(); // Converts IBuffer to byte[]
                        SwipeOutput.Text += $"Track 2 Raw: {Encoding.UTF8.GetString(track2Bytes)}\n";
                    }
                } else {
                    SwipeOutput.Text += "Swipe detected, but the data report was empty.\n";
                }
            });
        }

        // Triggers if they accidentally swipe a standard bank/credit card
        private void ClaimedReader_BankCardDataReceived(ClaimedMagneticStripeReader sender, MagneticStripeReaderBankCardDataReceivedEventArgs args) {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                SwipeOutput.Text = "=== BANK CARD SWIPE DETECTED ===\n";
                SwipeOutput.Text += $"Account Number: {args.AccountNumber}\n";
                SwipeOutput.Text += $"Cardholder Name: {args.FirstName} {args.Surname}\n";
            });
        }

        private void CopyStatus_Click(object sender, RoutedEventArgs e) {
            CopyToClipboard(StatusText.Text);
        }

        private void CopySwipe_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(SwipeOutput.Text)) {
                CopyToClipboard(SwipeOutput.Text);

                // Provide visual confirmation on screen for the swipe box
                var originalText = SwipeOutput.Text;
                if (!originalText.StartsWith("=== COPIED")) {
                    SwipeOutput.Text = "=== COPIED TO CLIPBOARD ===\n\n" + originalText;
                }
            }
        }

        // Helper method to interact with the Windows Clipboard API
        private void CopyToClipboard(string textToCopy) {
            if (!string.IsNullOrEmpty(textToCopy)) {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(textToCopy);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
    }
}

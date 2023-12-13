using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using XamlBrewer.WinUI3.BeerColorMeter.Models;

namespace XamlBrewer.WinUI3.BeerColorMeter
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            _ = Load();
        }

        private async Task Load()
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Beer.jpg"));
            await ImageCropper.LoadImageFromFile(file);
        }

        private async Task PickImage()
        {
            // Create a file picker
            var openPicker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // Set options
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            // Open the picker
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                await ImageCropper.LoadImageFromFile(file);
            }
        }

        private async void PickButton_Click(object sender, RoutedEventArgs e)
        {
            await PickImage();
        }

        private async void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a .png from the cropped image
            var stream = new InMemoryRandomAccessStream();
            await ImageCropper.SaveAsync(stream, BitmapFileFormat.Png);
            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            stream.Dispose();

            // Get the pixels
            BitmapTransform transform = new()
            {
                ScaledWidth = (uint)ImageCropper.CroppedRegion.Width,
                ScaledHeight = (uint)ImageCropper.CroppedRegion.Height
            };
            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage
            );

            // Calculate average color
            byte[] sourcePixels = pixelData.DetachPixelData();
            var nbrOfPixels = sourcePixels.Length / 4;
            int avgR = 0, avgG = 0, avgB = 0;
            for (int i = 0; i < sourcePixels.Length; i += 4)
            {
                avgR += sourcePixels[i];
                avgG += sourcePixels[i + 1];
                avgB += sourcePixels[i + 2];
            }

            var color = Color.FromArgb(255, (byte)(avgR / nbrOfPixels), (byte)(avgG / nbrOfPixels), (byte)(avgB / nbrOfPixels));
            Result.Background = new SolidColorBrush(color);

            // Calculate nearest beer color
            double distance = int.MaxValue;
            BeerColor closest = DAL.BeerColors[0];
            foreach (var beerColor in DAL.BeerColors)
            {
                double d = Math.Sqrt(Math.Pow(beerColor.B - color.B, 2)
                                   + Math.Pow(beerColor.G - color.G, 2)
                                   + Math.Pow(beerColor.R - color.R, 2));
                if (d < distance)
                {
                    distance = d;
                    closest = beerColor;
                }
            }

            ClosestBeerColor.Background = new SolidColorBrush(Color.FromArgb(255, closest.R, closest.G, closest.B));
            ClosestBeerColorText.Text = $"SRM: {(int)closest.SRM}{Environment.NewLine}ECB: {(int)closest.ECB}{Environment.NewLine}{Environment.NewLine}{closest.ColorName}";
            if (closest.ECB < 12 )
            {
                ClosestBeerColorText.Foreground = new SolidColorBrush(Colors.Maroon);
            }
            else
            {
                ClosestBeerColorText.Foreground = new SolidColorBrush(Colors.Beige);
            }
        }
    }
}

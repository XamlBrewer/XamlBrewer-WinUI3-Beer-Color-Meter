namespace XamlBrewer.WinUI3.BeerColorMeter
{
    using CommunityToolkit.WinUI.Controls;
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using Windows.Storage.Pickers;
    using Windows.Storage.Streams;
    using Windows.UI;
    using XamlBrewer.WinUI3.BeerColorMeter.Models;

    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            Title = "XAML Brewer WinUI 3 Beer Color Meter";
            InitializeComponent();
            AppWindow.SetIcon("Assets/Beer.ico");

            UseImageCropper = true;

            _ = Load();
        }

        private bool UseImageCropper
        {
            get
            {
                return ImageCropper.Visibility == Visibility.Visible;
            }

            set
            {
                if (value)
                {
                    ImageCropper.Visibility = Visibility.Visible;
                    FullImage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImageCropper.Visibility = Visibility.Collapsed;
                    FullImage.Visibility = Visibility.Visible;
                }
            }
        }

        private async Task Load()
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Beer.jpg"));
            await OpenFile(file);
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
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            // Open the picker
            var file = await openPicker.PickSingleFileAsync();
            await OpenFile(file);
        }

        private async Task OpenFile(StorageFile file)
        {
            if (file != null)
            {
                if (UseImageCropper)
                {
                    await ImageCropper.LoadImageFromFile(file);
                }
                else
                {
                    FullImage.Source = new BitmapImage(new Uri(file.Path));
                }
            }
        }

        private async void PickButton_Click(object sender, RoutedEventArgs e)
        {
            await PickImage();
        }

        private async void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            BitmapDecoder decoder;
            BitmapTransform transform;
            if (UseImageCropper)
            {
                // Create a .png from the cropped image
                var stream = new InMemoryRandomAccessStream();
                await ImageCropper.SaveAsync(stream, BitmapFileFormat.Png);
                stream.Seek(0);
                decoder = await BitmapDecoder.CreateAsync(stream);
                stream.Dispose();

                transform = new()
                {
                    ScaledWidth = (uint)ImageCropper.CroppedRegion.Width,
                    ScaledHeight = (uint)ImageCropper.CroppedRegion.Height
                };
            }
            else
            {
                var himage = (BitmapImage)FullImage.Source;
                var file = await StorageFile.GetFileFromPathAsync(himage.UriSour‌​ce.AbsoluteUri);

                using var stream = await file.OpenStreamForReadAsync();
                using var ras = stream.AsRandomAccessStream();
                decoder = await BitmapDecoder.CreateAsync(ras);

                transform = new()
                {
                    ScaledWidth = (uint)FullImage.ActualWidth,
                    ScaledHeight = (uint)FullImage.ActualHeight
                };
            }

            // Get the pixels
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

            DisplayResult(closest);
        }

        private void BeerColorSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            var closest = DAL.BeerColors.Where(c => c.SRM == e.NewValue).FirstOrDefault();
            if (closest != null)
            {
                DisplayResult(closest);
            }
        }

        private void DisplayResult(BeerColor closest)
        {
            ClosestBeerColor.Background = new SolidColorBrush(Color.FromArgb(255, closest.R, closest.G, closest.B));
            ClosestBeerColorText.Text = $"SRM: {(int)closest.SRM}{Environment.NewLine}EBC: {(int)closest.EBC}{Environment.NewLine}{Environment.NewLine}{closest.ColorName}";
            if (closest.EBC < 12)
            {
                ClosestBeerColorText.Foreground = new SolidColorBrush(Colors.Maroon);
            }
            else
            {
                ClosestBeerColorText.Foreground = new SolidColorBrush(Colors.Beige);
            }

            BeerColorSlider.Value = closest.SRM;
        }
    }
}

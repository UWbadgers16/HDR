using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Globalization;
using Windows.UI.Popups;
using Windows.Media.MediaProperties;
using Windows.Media.Devices;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Lumia.Imaging.Transforms;
using Lumia.Imaging;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra;
using ExifLib;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace HDR
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private MediaCapture mediaCapture;
        private ImageEncodingProperties imageEncoding;
        private DeviceInformation deviceInformation;
        private MessageDialog messageDialog;
        private FocusSettings focusSettings;
        private double firstExposureTime, secondExposureTime, thirdExposureTime, fourthExposureTime, fifthExposureTime;
        private int bufferSize;
        private bool autoISO = false;
        private int imageCount = 0;

		public MainPage()
		{
			this.InitializeComponent();

			this.NavigationCacheMode = NavigationCacheMode.Required;
		}

		/// <summary>
		/// Invoked when this page is about to be displayed in a Frame.
		/// </summary>
		/// <param name="e">Event data that describes how this page was reached.
		/// This parameter is typically used to configure the page.</param>
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			// TODO: Prepare page for display here.

			// TODO: If your application contains multiple pages, ensure that you are
			// handling the hardware Back button by registering for the
			// Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
			// If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.

            InitializeCamera();
		}
        
        private async void InitializeCamera()
        {
            mediaCapture = new MediaCapture();
            deviceInformation = await GetCameraDeviceInfoAsync(Windows.Devices.Enumeration.Panel.Back);

            var settings = new MediaCaptureInitializationSettings();
            settings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
            if (deviceInformation != null)
                settings.VideoDeviceId = deviceInformation.Id;

            await mediaCapture.InitializeAsync(settings);
            mediaCapture.VideoDeviceController.PrimaryUse = CaptureUse.Photo;

            imageEncoding = ImageEncodingProperties.CreateJpeg();

            focusSettings = new FocusSettings();
            focusSettings.AutoFocusRange = AutoFocusRange.FullRange;
            focusSettings.Mode = FocusMode.Auto;
            focusSettings.WaitForFocus = true;
            focusSettings.DisableDriverFallback = false;
            mediaCapture.VideoDeviceController.FocusControl.Configure(focusSettings);

            mediaCapture.VideoDeviceController.FlashControl.Enabled = false;

            mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);

            previewElement.Source = mediaCapture;
            await mediaCapture.StartPreviewAsync();
        }

        private static async Task<DeviceInformation> GetCameraDeviceInfoAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            DeviceInformation device = (await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture))
                .FirstOrDefault(d => d.EnclosureLocation != null && d.EnclosureLocation.Panel == desiredPanel);

            if (device == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No suitable devices found for the camera of type {0}.", desiredPanel));
            }

            return device;
        }

        private void HDR_High_Click(object sender, RoutedEventArgs e)
        {
            CaptureImage((float)2);
        }

        private void HDR_Medium_Click(object sender, RoutedEventArgs e)
        {
            CaptureImage((float)1.5);
        }

        private void HDR_Low_Click(object sender, RoutedEventArgs e)
        {
            CaptureImage((float)1);
        }

        private void ISO_Click(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            if (!autoISO)
                b.Content = "Auto ISO";
            else
                b.Content = "Min ISO";

            autoISO = !autoISO;
        }

		private async void CaptureImage(float EV)
        {
            List<IImageProvider> images = new List<IImageProvider>();
            await mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
            if(!autoISO)
                await mediaCapture.VideoDeviceController.IsoSpeedControl.SetValueAsync(mediaCapture.VideoDeviceController.IsoSpeedControl.Min);
            else
                await mediaCapture.VideoDeviceController.IsoSpeedControl.SetAutoAsync();

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Min);
            var saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            firstExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Min / 2);
            saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            secondExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(0);
            saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            thirdExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Max / 2);
            saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            fourthExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Max);
            saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            fifthExposureTime = saveImageResults.Item2;

            saveImageResults = null;
            imageCount = images.Count;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(0);

            List<IImageProvider> alignedImages = await AlignImages(images);
            images = null;

            for (int i = 0; i < imageCount; i++)
            {
                await WriteDataToFileAsync(i, await GetPixels(alignedImages[0]));
                alignedImages.RemoveAt(0);
            }
            alignedImages = null;

            messageDialog = new MessageDialog("Pixelated images saved to internal storage");
            await messageDialog.ShowAsync();


            List<byte[]> pixelImages = new List<byte[]>();
            for (int i = 0; i < imageCount; i++)
            {
                pixelImages.Add(await ReadFileContentsAsync(i.ToString()));
            }

            Stopwatch time = new Stopwatch();
            time.Start();
            List<Vector<double>> x_values = PerformHDR(pixelImages);
            time.Stop();

            messageDialog = new MessageDialog(time.Elapsed.TotalSeconds.ToString());
            await messageDialog.ShowAsync();
		}

        private async Task WriteDataToFileAsync(int image, byte[] data)
        {
            var folder = ApplicationData.Current.LocalFolder;

            var file = await folder.CreateFileAsync(image.ToString(), CreationCollisionOption.ReplaceExisting);

            using (var s = await file.OpenStreamForWriteAsync())
            {
                await s.WriteAsync(data, 0, data.Length);
            }

            bufferSize = data.Length;
            data = null;
            file = null;
            folder = null;
        }

        public async Task<byte[]> ReadFileContentsAsync(string fileName)
        {
            var folder = ApplicationData.Current.LocalFolder;

            try
            {
                using (var file = await folder.OpenStreamForReadAsync(fileName))
                {
                    byte[] data = new byte[bufferSize];
                    await file.ReadAsync(data, 0, bufferSize);

                    return data;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<Tuple<StreamImageSource, double>> SaveImage()
        {
            var photoStorageFile = await KnownFolders.CameraRoll.CreateFileAsync("photo.jpg", CreationCollisionOption.GenerateUniqueName);
            var fileStream = await photoStorageFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var imageStream = new InMemoryRandomAccessStream();
            await mediaCapture.CapturePhotoToStreamAsync(imageEncoding, imageStream);

            imageStream.Seek(0);
            var rotateDecoder = await BitmapDecoder.CreateAsync(imageStream);

            var memStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, rotateDecoder);

            encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception err)
            {
                switch (err.HResult)
                {
                    case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                        // If the encoder does not support writing a thumbnail, then try again
                        // but disable thumbnail generation.
                        encoder.IsThumbnailGenerated = false;
                        break;
                    default:
                        throw err;
                }
            }

            memStream.Seek(0);
            fileStream.Seek(0);
            fileStream.Size = 0;
            await RandomAccessStream.CopyAsync(memStream, fileStream);

            imageStream.Dispose();
            memStream.Dispose();

            fileStream.Seek(0);
            var stream = new InMemoryRandomAccessStream();
            stream.Seek(0);
            await RandomAccessStream.CopyAsync(fileStream, stream);

            fileStream.Seek(0);

            double exposureTime;
            int height;
            int width;
            using (ExifReader reader = new ExifLib.ExifReader(fileStream.AsStream()))
            {
                reader.GetTagValue<double>(ExifTags.ExposureTime, out exposureTime);
                reader.GetTagValue<int>(ExifTags.ImageLength, out height);
                reader.GetTagValue<int>(ExifTags.ImageWidth, out width);
            }

            /*ExifLib.JpegInfo info = ExifLib.ExifReader.ReadJpeg(fileStream.AsStream(), photoStorageFile.Name);
            double exposureTime = info.ExposureTime;*/
            fileStream.Dispose();

            stream.Seek(0);
            return Tuple.Create(new StreamImageSource(stream.AsStream()), exposureTime);
        }

        private async Task<List<IImageProvider>> AlignImages(List<IImageProvider> unalignedImages)
        {
            List<IImageProvider> alignedImages = new List<IImageProvider>();
            InvalidOperationException ioe = null;
            int count = 0;
            bool failed = false;

            try
            {
                using (var aligner = new ImageAligner())
                {
                    aligner.Sources = unalignedImages;
                    aligner.ReferenceSource = unalignedImages[imageCount/ 2];

                    var alignedSources = await aligner.AlignAsync();

                    foreach (var alignedSource in alignedSources)
                    {
                        if (alignedSource != null)
                        {
                            alignedImages.Add(alignedSource);
                            count++;
                        }
                    }

                    if (count  < imageCount)
                    {
                        messageDialog = new MessageDialog("Image alignment failed. Your HDR might not be well-aligned.");
                        await messageDialog.ShowAsync();
                        failed = true;
                    }
                    else
                    {
                        messageDialog = new MessageDialog("Image alignment complete");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                ioe = e;
            }
            if (ioe != null)
            {
                messageDialog = new MessageDialog("Image alignment failed. Your HDR might not be well-aligned.");
                await messageDialog.ShowAsync();
                failed = true;
            }

            if (failed)
            {
                alignedImages = null;
                return unalignedImages;
            }
            else
            {
                unalignedImages = null;
                return alignedImages;
            }
        }

        /*private async Task<List<byte[]>> GetPixels(List<IImageProvider> alignedSources)
        {
            List<byte[]> pixelImages = new List<byte[]>();

            foreach (var alignedSource in alignedSources)
            {
                if (alignedSource != null)
                {
                    using (var renderer = new BitmapRenderer())
                    {
                        renderer.Source = alignedSource;
                        Bitmap alignedBuffer = await renderer.RenderAsync();

                        pixelImages.Add(alignedBuffer.Buffers[0].Buffer.ToArray());
                    }
                }
            }

            alignedSources = null;
            return pixelImages;
        }*/

        private async Task<byte[]> GetPixels(IImageProvider alignedSource)
        {
            byte[] pixels = null;

            if (alignedSource != null)
            {
                using (var renderer = new BitmapRenderer())
                {
                    renderer.Source = alignedSource;
                    Bitmap alignedBuffer = await renderer.RenderAsync();

                    pixels = alignedBuffer.Buffers[0].Buffer.ToArray();
                }
            }

            alignedSource = null;
            return pixels;
        }

        private List<Vector<double>> PerformHDR(List<byte[]> images)
        {
            List<Vector<double>> x_values = new List<Vector<double>>();
            int[] samples = Sample(images[imageCount/ 2]);

            Matrix<double> A_blue= Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_blue = Vector<double>.Build.Dense(A_blue.RowCount, 0);
            Matrix<double> A_green = Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_green = Vector<double>.Build.Dense(A_green.RowCount, 0);
            Matrix<double> A_red = Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_red = Vector<double>.Build.Dense(A_red.RowCount, 0);

            double lambda = 200;
            double weight = 0;
            byte[] image = null;
            UInt16 value = 0;
            int k = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                for (int j = 0; j < imageCount; j++)
                {
                    image = images[j];

                    value = Convert.ToUInt16(image[samples[i] * 4 ]);
                    weight = GetWeight(value);
                    A_blue[k, value] = weight;
                    A_blue[k, 256 + i] = -weight;
                    b_blue[k] = weight * GetExposureTime(j);

                    value = Convert.ToUInt16(image[samples[i] * 4 + 1]);
                    weight = GetWeight(value);
                    A_green[k, value] = weight;
                    A_green[k, 256 + i] = -weight;
                    b_green[k] = weight * GetExposureTime(j);

                    value = Convert.ToUInt16(image[samples[i] * 4 + 2]);
                    weight = GetWeight(value);
                    A_red[k, value] = weight;
                    A_red[k, 256 + i] = -weight;
                    b_red[k] = weight * GetExposureTime(j);

                    k++;
                }
            }

            A_blue[k, 128] = 1;
            A_green[k, 128] = 1;
            A_red[k, 128] = 1;
            k++;

            for (int i = 1; i <= 254; i++)
            {
                weight = GetWeight(Convert.ToUInt16(i));
                A_blue[k, i - 1] = lambda * weight;
                A_blue[k, i] = -2 * lambda * weight;
                A_blue[k, i + 1] = lambda * weight;

                A_green[k, i - 1] = lambda * weight;
                A_green[k, i] = -2 * lambda * weight;
                A_green[k, i + 1] = lambda * weight;

                A_red[k, i - 1] = lambda * weight;
                A_red[k, i] = -2 * lambda * weight;
                A_red[k, i + 1] = lambda * weight;

                k++;
            }

            Svd<double> svd = A_blue.Svd(true);
            x_values.Add(svd.Solve(b_blue));

            svd = A_green.Svd(true);
            x_values.Add(svd.Solve(b_green));

            svd = A_red.Svd(true);
            x_values.Add(svd.Solve(b_red));

            A_blue = null;
            b_blue = null;
            A_green = null;
            b_green = null;
            A_red = null;
            b_red = null;
            samples = null;
            image = null;
            images = null;

            return x_values;
        }

        private int[] Sample(byte[] image)
        {
            int[] samples = new int[128];

            int range = image.Length / 4;
            Random rand = new Random();
            int sample = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                sample = rand.Next(range);

                while (Convert.ToUInt16(image[sample * 4]) < 5 || Convert.ToUInt16(image[sample * 4]) > 250 || Convert.ToUInt16(image[sample * 4 + 1]) < 5 
                    || Convert.ToUInt16(image[sample * 4 + 1]) > 250 || Convert.ToUInt16(image[sample * 4 + 2]) < 5 || Convert.ToUInt16(image[sample * 4 + 2]) > 250)
                {
                    sample = rand.Next(range);
                }

                samples[i] = sample;
            }

            return samples;
        }

        /*private List<int[]> Sample(byte[] image)
        {
            List<int[]> samples = new List<int[]>();
            int[] blueSamples = new int[128];
            int[] greenSamples = new int[128];
            int[] redSamples = new int[128];

            int range = image.Length / 4;
            Random rand = new Random();
            int sample = 0;

            for (int i = 0; i < blueSamples.Length; i++)
            {
                sample = rand.Next(range);

                while (Convert.ToUInt16(image[sample * 4]) < 5 || Convert.ToUInt16(image[sample * 4]) > 250)
                {
                    sample = rand.Next(range); 
                }

                blueSamples[i] = sample;
            }

            for (int i = 0; i < greenSamples.Length; i++)
            {
                sample = rand.Next(range);

                while (Convert.ToUInt16(image[(sample * 4) + 1]) < 5 || Convert.ToUInt16(image[(sample * 4) + 1]) > 250)
                {
                    sample = rand.Next(range);
                }

                greenSamples[i] = sample;
            }

            for (int i = 0; i < redSamples.Length; i++)
            {
                sample = rand.Next(range);

                while (Convert.ToUInt16(image[(sample * 4) + 2]) < 5 || Convert.ToUInt16(image[(sample * 4) + 2]) > 250)
                {
                    sample = rand.Next(range);
                }

                redSamples[i] = sample;
            }

            samples.Add(blueSamples);
            samples.Add(greenSamples);
            samples.Add(redSamples);

            return samples;
        }*/

        private double GetWeight(UInt16 value)
        {
           return ((Convert.ToDouble(value) <= 127) ? Convert.ToDouble(value) : (255 - Convert.ToDouble(value)));
        }

        private double GetExposureTime(int image)
        {
            switch(image)
            {
                case 0:
                    return (Math.Log(firstExposureTime));
                case 1:
                    return (Math.Log(secondExposureTime));
                case 2:
                    return (Math.Log(thirdExposureTime));
                default:
                    return 0;
            }
        }
	}
}

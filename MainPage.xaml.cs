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
using System.Text;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace HDR
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
        /* class variables */
		private MediaCapture mediaCapture;                                                                                  //media capture for taking photos
        private ImageEncodingProperties imageEncoding;                                                                      //encoding for saving jpegs
        private DeviceInformation deviceInformation;                                                                        //camera device information
        private MessageDialog messageDialog;                                                                                //for message dialogs
        private FocusSettings focusSettings;                                                                                //focus settings
        private double firstExposureTime, secondExposureTime, thirdExposureTime, fourthExposureTime, fifthExposureTime;     //exposure times saved for HDF
        private UInt32 height, width;                                                                                       //image height and width
        private int bufferSize;                                                                                             //buffer size to read file                           
        private int imageCount = 0;                                                                                         //number of images used

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

            InitializeCamera();                                                                                             //initialize the camera
		}
        
        /* initializes the camera */
        private async void InitializeCamera()
        {
            mediaCapture = new MediaCapture();                                                                              //initialize the media capture
            deviceInformation = await GetCameraDeviceInfoAsync(Windows.Devices.Enumeration.Panel.Back);                     //get the rear camera

            var settings = new MediaCaptureInitializationSettings();                                                        //set up viewfinder
            settings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
            if (deviceInformation != null)
                settings.VideoDeviceId = deviceInformation.Id;

            await mediaCapture.InitializeAsync(settings);                                                                   //initialize the media capture
            mediaCapture.VideoDeviceController.PrimaryUse = CaptureUse.Photo;

            imageEncoding = ImageEncodingProperties.CreateJpeg();                                                           //set to jpeg encoding

            focusSettings = new FocusSettings();                                                                            //set up the focus
            focusSettings.AutoFocusRange = AutoFocusRange.FullRange;
            focusSettings.Mode = FocusMode.Auto;
            focusSettings.WaitForFocus = true;
            focusSettings.DisableDriverFallback = false;
            mediaCapture.VideoDeviceController.FocusControl.Configure(focusSettings);

            mediaCapture.VideoDeviceController.FlashControl.Enabled = false;                                                //turn off flash

            mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);                                              //set preview for portrait

            previewElement.Source = mediaCapture;                                               
            await mediaCapture.StartPreviewAsync();                                                                         //start the viewfinder
        }

        /* gets rear camera */
        private static async Task<DeviceInformation> GetCameraDeviceInfoAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            DeviceInformation device = (await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture))
                .FirstOrDefault(d => d.EnclosureLocation != null && d.EnclosureLocation.Panel == desiredPanel);             //get rear camera

            if (device == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "No suitable devices found for the camera of type {0}.", desiredPanel));
            }

            return device;
        }

        /* capture click event handler */
        private void Capture_Click(object sender, RoutedEventArgs e)
        {
            CaptureImage();                                                                                                 //capture the image
        }

        /* captures the image and initiates processing */
		private async void CaptureImage()
        {
            List<IImageProvider> images = new List<IImageProvider>();                                                       //list of images
            await mediaCapture.VideoDeviceController.FocusControl.FocusAsync();                                             //auto focus
            await mediaCapture.VideoDeviceController.IsoSpeedControl.SetAutoAsync();                                        //auto iso

            /* change exposure of each image, capture images, add to list, and save the image's exposure time */
            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Min);
            var saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            firstExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(mediaCapture.VideoDeviceController.ExposureCompensationControl.Min / 2);
            saveImageResults = await SaveImage();
            images.Add(saveImageResults.Item1);
            secondExposureTime = saveImageResults.Item2;

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(0);
            mediaCapture.VideoDeviceController.Exposure.TrySetValue(0);
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

            await mediaCapture.VideoDeviceController.ExposureCompensationControl.SetValueAsync(0);                          //set back to middle exposure
                    
            List<IImageProvider> alignedImages = await AlignImages(images);                                                 //try to align the images
            images = null;

            /* get the pixel representations of images and save to isolate storage */
            for (int i = 0; i < imageCount; i++)
            {
                await WriteImageToFileAsync(i, await GetPixels(alignedImages[0]));
                alignedImages.RemoveAt(0);
            }

            messageDialog = new MessageDialog("Pixelated images saved to internal storage");
            await messageDialog.ShowAsync();

            /* read the images from isolated storage */
            List<byte[]> pixelImages = new List<byte[]>();
            for (int i = 0; i < imageCount; i++)
            {
                pixelImages.Add(await ReadFileContentsAsync(i.ToString()));
            }

            Stopwatch time = new Stopwatch();                                                                               //set up and start stopwatch
            time.Start();
            List<Vector<double>> x_values = PerformHDR(pixelImages);                                                        //perform HDR
            time.Stop();                                                                                                    //stop stopwatch

            messageDialog = new MessageDialog("SVD solved in " + time.Elapsed.TotalSeconds.ToString() + " seconds");
            await messageDialog.ShowAsync();

            List<Vector<double>> g_values = new List<Vector<double>>();                                                     //save response functions to lists
            g_values.Add(x_values[0].SubVector(0,256));
            g_values.Add(x_values[1].SubVector(0,256));
            g_values.Add(x_values[2].SubVector(0,256));
            x_values = null;

            WriteResponseFunctionFile(g_values[0], "blue");                                                                 //write response functions to isolated storage
            WriteResponseFunctionFile(g_values[1], "green");
            WriteResponseFunctionFile(g_values[2], "red");

            byte[] radianceMap = RadianceMap(pixelImages, g_values);                                                        //develop radiance map
            pixelImages = null;
            g_values = null;

            WriteHDRFile(radianceMap);                                                                                      //write the radiance map to isolated storage

            messageDialog = new MessageDialog("Radiance map complete");
            await messageDialog.ShowAsync();
		}

        /*  writes an .hdr file to isolated storage */
        private async void WriteHDRFile(byte[] radianceMap)
        {
            var folder = ApplicationData.Current.LocalFolder;                                                               //open folder and create file
            var file = await folder.CreateFileAsync("radiance_map.hdr", CreationCollisionOption.GenerateUniqueName);

            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (IOutputStream outputStream = fileStream.GetOutputStreamAt(0))
                {
                    using (DataWriter dataWriter = new DataWriter(outputStream))
                    {
                        /* write the .hdr header */
                        dataWriter.WriteString("#?RADIANCE\n");
                        await dataWriter.StoreAsync();
                        dataWriter.WriteString("pvalue -s 15 -h -df -r -y " + height.ToString() + " +x " + width.ToString() + "\n");
                        await dataWriter.StoreAsync();
                        dataWriter.WriteString("FORMAT=32-bit_rle_rgbe\n\n");
                        await dataWriter.StoreAsync();
                        dataWriter.WriteString("-Y " + height.ToString() + " +X " + width.ToString() + "\n");
                        await dataWriter.StoreAsync();
                        dataWriter.WriteBuffer(radianceMap.AsBuffer(), 0, Convert.ToUInt32(radianceMap.Length));
                        await dataWriter.StoreAsync();
                    }
                }
            }
        }

        /* writes a response function file to isolated storage */
        private async void WriteResponseFunctionFile(Vector<double> response, String color)
        {
            var folder = ApplicationData.Current.LocalFolder;                                                               //open folder and create file
            var file = await folder.CreateFileAsync(color + ".resp", CreationCollisionOption.ReplaceExisting);


            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (IOutputStream outputStream = fileStream.GetOutputStreamAt(0))
                {
                    using (DataWriter dataWriter = new DataWriter(outputStream))
                    {
                        for(int i = 0; i < response.Count; i++)
                        {
                            /* write the response function item */
                            dataWriter.WriteString(response[i].ToString());
                            await dataWriter.StoreAsync();
                            dataWriter.WriteString("\n");
                            await dataWriter.StoreAsync();
                        }
                    }
                }
            }
        }

        /* writes image to isolated storage */
        private async Task WriteImageToFileAsync(int image, byte[] data)
        {
            var folder = ApplicationData.Current.LocalFolder;                                                             //open folder and create file
            var file = await folder.CreateFileAsync(image.ToString() + ".dat", CreationCollisionOption.ReplaceExisting);

            /* write image */
            using (var s = await file.OpenStreamForWriteAsync())
            {
                await s.WriteAsync(data, 0, data.Length);
            }

            bufferSize = data.Length;
            data = null;
            file = null;
            folder = null;
        }

        /* read image from isolated storage */
        public async Task<byte[]> ReadFileContentsAsync(string fileName)
        {
            var folder = ApplicationData.Current.LocalFolder;                                                               //open folder

            try
            {
                /* open folder and read into buffer */
                using (var file = await folder.OpenStreamForReadAsync(fileName + ".dat"))
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

        /* save an image to the camera roll and get the exposure time */
        private async Task<Tuple<StreamImageSource, double>> SaveImage()
        {  
            /* create a new image file */
            var photoStorageFile = await KnownFolders.CameraRoll.CreateFileAsync("photo.jpg", CreationCollisionOption.GenerateUniqueName);
            var fileStream = await photoStorageFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var imageStream = new InMemoryRandomAccessStream();
            await mediaCapture.CapturePhotoToStreamAsync(imageEncoding, imageStream);

            /* use bitmap decoder and encoder to rotate image 90 degrees for proper viewing */
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
            await RandomAccessStream.CopyAsync(memStream, fileStream);                                                      //save image

            imageStream.Dispose();
            memStream.Dispose();

            fileStream.Seek(0);
            var stream = new InMemoryRandomAccessStream();
            stream.Seek(0);
            await RandomAccessStream.CopyAsync(fileStream, stream);

            fileStream.Seek(0);

            /* read the exposure time from exif tag */
            double exposureTime;
            using (ExifReader reader = new ExifLib.ExifReader(fileStream.AsStream()))
            {
                reader.GetTagValue<double>(ExifTags.ExposureTime, out exposureTime);
            }

            fileStream.Dispose();

            stream.Seek(0);
            return Tuple.Create(new StreamImageSource(stream.AsStream()), exposureTime);                                    //return image stream and exposure time
        }

        /* align the images */
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
                    aligner.Sources = unalignedImages;                                                                      //set sources for alignment
                    aligner.ReferenceSource = unalignedImages[imageCount / 2];                                              //set reference to the 0 EV image

                    var alignedSources = await aligner.AlignAsync();                                                        //try aligning the images

                    foreach (var alignedSource in alignedSources)
                    {
                        if (alignedSource != null)
                        {
                            alignedImages.Add(alignedSource);                                                               //add aligned images
                            count++;
                        }
                    }

                    /* not all images could be aligned */
                    if (count  < imageCount)
                    {
                        messageDialog = new MessageDialog("Image alignment failed. Your HDR might not be well-aligned.");
                        await messageDialog.ShowAsync();
                        failed = true;
                    }
                    /* all images were aligned */
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

        /* get pixel images for all images */
        private async Task<List<byte[]>> GetPixelsAll(List<IImageProvider> alignedSources)
        {
            List<byte[]> pixelImages = new List<byte[]>();

            foreach (var alignedSource in alignedSources)
            {
                if (alignedSource != null)
                {
                    using (var renderer = new BitmapRenderer())
                    {
                        /* get the pixel image */
                        renderer.Source = alignedSource;
                        Bitmap alignedBuffer = await renderer.RenderAsync();

                        pixelImages.Add(alignedBuffer.Buffers[0].Buffer.ToArray());
                    }
                }
            }

            alignedSources = null;
            return pixelImages;
        }

        /* get pixel images for one image and save image height and width*/
        private async Task<byte[]> GetPixels(IImageProvider alignedSource)
        {
            byte[] pixels = null;

            if (alignedSource != null)
            {
                using (var renderer = new BitmapRenderer())
                {
                    /* get the pixel image and store height and width */
                    renderer.Source = alignedSource;
                    Bitmap alignedBuffer = await renderer.RenderAsync();
                    height = Convert.ToUInt32(alignedBuffer.Dimensions.Height);
                    width = Convert.ToUInt32(alignedBuffer.Dimensions.Width);

                    pixels = alignedBuffer.Buffers[0].Buffer.ToArray();
                }
            }

            alignedSource = null;
            return pixels;
        }

        /* performs the main HDR functions */
        private List<Vector<double>> PerformHDR(List<byte[]> images)
        {
            List<Vector<double>> x_values = new List<Vector<double>>();                                                             //x result vectors for R, G, and B
            int[] samples = Sample(images[imageCount / 2]);                                                                         //get the sampled pixels from 0 EV image

            /* initialize A and b matrices */
            Matrix<double> A_blue= Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_blue = Vector<double>.Build.Dense(A_blue.RowCount, 0);
            Matrix<double> A_green = Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_green = Vector<double>.Build.Dense(A_green.RowCount, 0);
            Matrix<double> A_red = Matrix<double>.Build.Dense(samples.Length * images.Count + 256 + 1, 256 + samples.Length, 0);
            Vector<double> b_red = Vector<double>.Build.Dense(A_red.RowCount, 0);

            double lambda = 400;                                                                                                    //value of lambda
            double weight = 0;                                                                                                      //initialize temporary variables
            byte[] image = null;
            UInt16 value = 0;
            int k = 0;

            /* loop through all samples */
            for (int i = 0; i < samples.Length; i++)
            {
                /* loop through all images */
                for (int j = 0; j < imageCount; j++)
                {
                    /* Debevec et al. HDR algorithm setting up linear least squares system */
                    image = images[j];

                    value = Convert.ToUInt16(image[samples[i] * 4]);
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

            /* set midpoints to 0 */
            A_blue[k, 128] = 1;
            A_green[k, 128] = 1;
            A_red[k, 128] = 1;
            k++;

            /* add smoothness terms */
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

            /* perform singular value decomposition for R, G, and B and solve for x*/
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

            return x_values;                                                                                                        //return the x vectors for R, G, and B
        }

        /* compute the radiance map */
        private byte[] RadianceMap(List<byte[]> images, List<Vector<double>> g_values)
        {
            /* initialize temporary variables */
            byte[] image = null;
            double blueNumerator = 0;
            double blueDenominator = 0;
            double greenNumerator = 0;
            double greenDenominator = 0;
            double redNumerator = 0;
            double redDenominator = 0;
            double blueRadiance = 0;
            double greenRadiance = 0;
            double redRadiance = 0;
            UInt16 value = 0;
            double weight = 0;
            Vector<double> g = null;
            int range = images[0].Length / 4;
            byte[] radianceMap = new byte[range * 4];

            /* loop through entire radiance map */
            for (int i = 0; i < range; i++)
            {
                //loop through all images
                for (int j = 0; j < imageCount; j++)
                {
                    image = images[j];

                    /* for each color, compute numerator and denominator of weighted average of this pixel at all images */
                    value = Convert.ToUInt16(image[i * 4]);
                    weight = GetWeight(value);
                    g = g_values[0];
                    blueNumerator += weight * (g[value] - GetExposureTime(j));
                    blueDenominator += weight;

                    value = Convert.ToUInt16(image[(i * 4) + 1]);
                    weight = GetWeight(value);
                    g = g_values[1];
                    greenNumerator += weight * (g[value] - GetExposureTime(j));
                    greenDenominator += weight;

                    value = Convert.ToUInt16(image[(i * 4) + 2]);
                    weight = GetWeight(value);
                    g = g_values[2];
                    redNumerator += weight * (g[value] - GetExposureTime(j));
                    redDenominator += weight;
                }

                /* find the radiance values of each color of this pixel */
                blueRadiance = Math.Pow(Math.E, (blueNumerator / blueDenominator));
                greenRadiance = Math.Pow(Math.E, (greenNumerator / greenDenominator));
                redRadiance = Math.Pow(Math.E, (redNumerator / redDenominator));

                /* get RGBE values for this pixel and save to the radiance map */
                byte[] rgbe = new byte[4];
                GetRGBE(ref rgbe, blueRadiance, greenRadiance, redRadiance);
                radianceMap[i * 4] = rgbe[0];
                radianceMap[i * 4 + 1] = rgbe[1];
                radianceMap[i * 4 + 2] = rgbe[2];
                radianceMap[i * 4 + 3] = rgbe[3];

                blueNumerator = 0;
                blueDenominator = 0;
                greenNumerator = 0;
                greenDenominator = 0;
                redNumerator = 0;
                redDenominator = 0;
            }

            return radianceMap;                                                                                                     //return the radiance map
        }

        /* get the RGBE values for a pixel */
        private void GetRGBE(ref byte[] rgbe, double blue, double green, double red)
        {
            double v;
            int e = 0;

            /* find brightest of RGB */
            v = red;
            if (green > v) v = green;
            if (blue > v) v = blue;
            if (v < 1e-32)
            {
                rgbe[0] = rgbe[1] = rgbe[2] = rgbe[3] = 0;
            }
            else
            {
                /* get the mantissa and exponent of the brightest and scale the values */
                v = frexp(v, ref e) * 256.0 / v;
                rgbe[0] = (byte)(red * v);
                rgbe[1] = (byte)(green * v);
                rgbe[2] = (byte)(blue * v);
                rgbe[3] = (byte)(e + 128);
            }
        }

        /* gets the exponent and mantissa of the floating point number */
        private double frexp(double x, ref int exp)
        {
            exp = (int)Math.Floor(Math.Log(x) / Math.Log(2)) + 1;
            return 1 - (Math.Pow(2, exp) - x) / Math.Pow(2, exp);
        }

        /* sample the image to get sampled pixel locations */
        private int[] Sample(byte[] image)
        {
            int[] samples = new int[128];                                                                                           //initialize samples array

            /* initialize temporary variables */
            int range = image.Length / 4;
            Random rand = new Random();
            int sample = 0;

            /* loop through all samples */
            for (int i = 0; i < samples.Length; i++)
            {
                sample = rand.Next(range);                                                                                          //value to sample

                /* sample until the value is between 5-250 for all R, G, and B to guarantee good saturation */
                while (Convert.ToUInt16(image[sample * 4]) < 5 || Convert.ToUInt16(image[sample * 4]) > 250 || Convert.ToUInt16(image[sample * 4 + 1]) < 5 
                    || Convert.ToUInt16(image[sample * 4 + 1]) > 250 || Convert.ToUInt16(image[sample * 4 + 2]) < 5 || Convert.ToUInt16(image[sample * 4 + 2]) > 250)
                {
                    sample = rand.Next(range);
                }

                samples[i] = sample;                                                                                                //add sample
            }   

            return samples;                                                                                                         //return sampled locations
        }

        /* get the weight of a pixel value */
        private double GetWeight(UInt16 value)
        {
           return ((Convert.ToDouble(value) <= 127) ? Convert.ToDouble(value) : (255 - Convert.ToDouble(value)));                   //get the weight of the pixel value
        }

        /* get the natural log of the exposure time */
        private double GetExposureTime(int image)
        {  
            /* return the natural log of the image's exposure time */
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

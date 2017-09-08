using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace PictureAState
{
    
    public sealed partial class MainPage : Page
    {
        MediaCapture camStream;
        bool _isPreviewing;
        DisplayRequest _displayRequest = new DisplayRequest();
        Image currentFilter = null;

        IReadOnlyList<StorageFile> filters = null;
        int currentFilterIndex = 0;

        bool imageCaptured = false;
        bool canAddFilter = false; // allow up to 5 filters to be added to one photo
        int filterCount = 0;

        public MainPage()
        {
            this.InitializeComponent();

            //SetupCamera();

            //Application.Current.Suspending += Current_Suspending;

            RectangleGeometry bounds = new RectangleGeometry();
            //bounds.Rect = new Rect(0, 0, this.renderTarget.Width, this.renderTarget.Height);
            //this.renderTarget.Clip = bounds;

        }

        private async void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanUp();
                deferral.Complete();
            }
        }

        private async void SetupCamera()
        {

            try
            {
                camStream = new MediaCapture();
                await camStream.InitializeAsync();

                _displayRequest.RequestActive();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            try
            {
                camView.Source = camStream;
                await camStream.StartPreviewAsync();
                _isPreviewing = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private async void ApplyFilter(object sender, RoutedEventArgs e)
        {
            if (canAddFilter == false)
                return;

            StorageFolder assetsFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
            StorageFolderQueryResult results = assetsFolder.CreateFolderQuery();
            IReadOnlyList<StorageFolder> folders = await results.GetFoldersAsync();

            StorageFolder filterFolder = folders.Where(fol => fol.DisplayName == "Filters").First();
            filters = await filterFolder.GetFilesAsync();

            /*Debug.WriteLine("Displaying Folder");
            Debug.WriteLine(filterFolder.DisplayName);

            Debug.WriteLine("Displaying filter names");

            foreach (StorageFile fil in filters)
            {
                Debug.WriteLine(fil.DisplayName);
            }*/


            Image filter = new Image();
            filter.VerticalAlignment = VerticalAlignment.Center;
            filter.HorizontalAlignment = HorizontalAlignment.Center;

            BitmapImage bm = new BitmapImage(new Uri(filters[0].Path));
            filter.Source = bm;

            Canvas.SetZIndex(filter, 1);
            this.renderTarget.Children.Add(filter);

            currentFilter = filter;
            filterCount++;
            canAddFilter = (filterCount == 5) ? !canAddFilter : canAddFilter;

            if (canAddFilter == false)
            {
                Debug.WriteLine("Cannot apply filter");
            }
            
        }

        private void LoadFilters()
        {

        }

        private async Task CleanUp()
        {
            if (camStream != null)
            {
                if (_isPreviewing)
                {
                    await camStream.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    camView.Source = null;

                    if (_displayRequest != null)
                    {
                        _displayRequest.RequestRelease();
                    }

                    camStream.Dispose();
                    camStream = null;
                });
            }
        }

        private async void StopStream(object sender, RoutedEventArgs e)
        {
            await CleanUp();
        }

        private void StartStream(object sender, RoutedEventArgs e)
        {
            SetupCamera();

            Application.Current.Suspending += Current_Suspending;
        }

        private void MoveFilterUp(object sender, RoutedEventArgs e)
        {
            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left,
                                               currentFilter.Margin.Top - 5,
                                               currentFilter.Margin.Right,
                                               currentFilter.Margin.Bottom + 5);
            currentFilter.Margin = thickems;
        }

        private void MoveFilterDown(object sender, RoutedEventArgs e)
        {
            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left,
                                               currentFilter.Margin.Top + 5,
                                               currentFilter.Margin.Right,
                                               currentFilter.Margin.Bottom - 5);
            currentFilter.Margin = thickems;
        }

        private void MoveFilterLeft(object sender, RoutedEventArgs e)
        {
            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left - 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right + 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = thickems;
        }

        private void MoveFilterRight(object sender, RoutedEventArgs e)
        {
            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left + 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right - 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = thickems;
        }

        private void IncreaseFilterScale(object sender, RoutedEventArgs e)
        {

            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left - 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right - 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = thickems;
        }

        private void DecreaseFilterScale(object sender, RoutedEventArgs e)
        {
            if (currentFilter == null)
                return;
            //Image filter = currentFilter;
            Thickness thickems = new Thickness(currentFilter.Margin.Left + 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right + 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = thickems;
        }

        private void RotateFilterLeft(object sender, RoutedEventArgs e)
        {
            return;
            // use rotate transfom
            // get current rotation and do stuff
        }

        private void RotateFilterRight(object sender, RoutedEventArgs e)
        {
            return;
            // use rotate transfom
            // get current rotation and do stuff
        }

        private async void SaveImage(object sender, RoutedEventArgs e)
        {
            if (imageCaptured == false)
                return;

            // location:
            // C:\Users\Beasley\AppData\Local\Packages\7042ff83-dc0c-4816-a1a0-107ef134b815_zx9gxz867859y\LocalState
            StorageFolder assetsFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFile file = null;

            try
            {
                file = await assetsFolder.CreateFileAsync("captured-photo.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            RenderTargetBitmap targetBitmap = new RenderTargetBitmap();
            await targetBitmap.RenderAsync(this.renderTarget);

            await CleanUp();

            foreach (UIElement child in this.renderTarget.Children)
            {
                if (child != this.camView)
                    this.renderTarget.Children.Remove(child);
            }

            this.renderTarget.Children.Remove(currentFilter);
            currentFilter = null;

            this.renderedImage.Source = targetBitmap;
            Image newImage = new Image();
            newImage.Source = targetBitmap;
            this.renderTarget.Children.Add(newImage);

            if (file != null)
            {
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    IBuffer targetBuffer = await targetBitmap.GetPixelsAsync();
                    SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, targetBitmap.PixelWidth, targetBitmap.PixelHeight);
                    softwareBitmap.CopyFromBuffer(targetBuffer);
                    encoder.SetSoftwareBitmap(softwareBitmap);

                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }

        }

        private async void CaptureImage(object sender, RoutedEventArgs e)
        {
            
            if (camStream == null)
                return;

            await Task.Delay(500);
            this.photoTimer.Text = "3";
            await Task.Delay(1000);
            this.photoTimer.Text = "2";
            await Task.Delay(1000);
            this.photoTimer.Text = "1";
            await Task.Delay(1000);
            this.photoTimer.Text = "Wolves Up";
            await Task.Delay(500);

            RenderTargetBitmap targetBitmap = new RenderTargetBitmap();
            await targetBitmap.RenderAsync(this.renderTarget);

            await CleanUp();

            this.renderedImage.Source = targetBitmap;
            Image newImage = new Image();
            newImage.Source = targetBitmap;
            this.renderTarget.Children.Add(newImage);

            canAddFilter = true;
            imageCaptured = true;

        }

        private void GetNextFilter(object sender, RoutedEventArgs e)
        {
            if (filters == null || filters.Count == 0)
                return;
            
            // check if currentFilterIndex != filters.Count - 1
            // if not, set the currentFilter to the next filter
            // increase fiilter count

        }

        private void GetPrevFilter(object sender, RoutedEventArgs e)
        {
            if (filters == null || filters.Count == 0)
                return;

            // check if currentFilterIndex != 0
            // if not, set the currentFilter to the previous filter
            // decrease fiilter count
        }
    }
}

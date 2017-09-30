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
using Windows.Graphics.Printing;
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
using Windows.UI.Xaml.Printing;

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
        Image capturedImage = new Image();

        // replace with Key,Value list
        bool isRegistered = false;
        UIElement[] appliedFilters = new UIElement[5];
        PrintDocument printDoc = new PrintDocument();

        IPrintDocumentSource printDocSource;


        public MainPage()
        {
            this.InitializeComponent();

            //SetupCamera();
            //Application.Current.Suspending += Current_Suspending;

            LoadFilters(); // load the filters that can be added to images

        }

        /// <summary>
        /// If the application is exiting/in the background/ or closing 
        /// dissconnect from the camera and unregister print helpers
        /// </summary>
        /// <param name="sender">The application window</param>
        /// <param name="e">Argumets for suspending the application</param>
        private async void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanUp();
                deferral.Complete();
            }

            UnregisterForPrinting();

        }

        /// <summary>
        /// Add handlers for the needed/required print functions
        /// </summary>
        private void RegisterForPrinting()
        {

            printDoc.Paginate += PrintDocPaginate; // set/send/register the page(s) to send to the printer
            printDoc.AddPages += PrintDocAddPages; // add to the list of pages that will be printed
            printDocSource = printDoc.DocumentSource; // track the document that will be printed

            PrintManager printMan = PrintManager.GetForCurrentView(); // print manager, registers for access to the printer
            printMan.PrintTaskRequested += PrintManPrintTaskRequested; // handle print requests
        }

        /// <summary>
        /// Remove handlers from the print variables
        /// </summary>
        private void UnregisterForPrinting()
        {

            printDoc.Paginate -= PrintDocPaginate;
            printDoc.AddPages -= PrintDocAddPages;

            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintManPrintTaskRequested;
        }

        /// <summary>
        /// Register to use the camera and open the stream to display in the application
        /// </summary>
        private async void SetupCamera()
        {
            // If the user has already taken an image
            // clear the view and reset all filter variables
            if(imageCaptured)
            {
                ClearView();

                currentFilter = null;
                currentFilterIndex = 0;
                imageCaptured = false;
                canAddFilter = false;
                filterCount = 0;
                appliedFilters = new UIElement[5];
            }

            try
            {
                // initialize the camera and request access
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
                // attach the cameras stream to the UI and start the preview
                camView.Source = camStream;
                await camStream.StartPreviewAsync();
                _isPreviewing = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Stop the camera stream and remove it from the UI
        /// </summary>
        /// <returns></returns>
        private async Task CleanUp()
        {
            if (camStream != null)
            {
                if (_isPreviewing)
                {
                    await camStream.StopPreviewAsync();
                }

                // Remove the steam, release the camera, and dispose of the stream
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

        /// <summary>
        /// Add filters to the UI for the user to edit
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            // return if the user shouldn't be able to add filters
            if (canAddFilter == false || filters.Count == 0)
                return;

            // create a new UI element for the filter and set the current filter
            Image filter = new Image();
            filter = currentFilter;

            // add to the filters that have been applied
            appliedFilters[filterCount] = filter;
            filterCount++;
            currentFilter = null;

            // if the user has added 5 filters, don't allow any more features
            canAddFilter = (filterCount == 5) ? !canAddFilter : canAddFilter;

            // let the user know they've reached the limit for filters
            if (canAddFilter == false)
            {
                Debug.WriteLine("Cannot apply filter");
                this.messageText.Text = "Filter Limit Reached";
                this.renderTarget.Children.Remove(currentFilter);
            }
            else
            {

                // add a new filter to the UI
                currentFilter = new Image();
                currentFilter.VerticalAlignment = VerticalAlignment.Center;
                currentFilter.HorizontalAlignment = HorizontalAlignment.Center;
                Canvas.SetZIndex(currentFilter, 1);

                BitmapImage bm = new BitmapImage(new Uri(filters[currentFilterIndex].Path));
                currentFilter.Source = bm;

                this.renderTarget.Children.Add(currentFilter);

            }

        }

        /// <summary>
        /// Grab all the the filters that are available
        /// </summary>
        private async void LoadFilters()
        {
            // open the local assets folder, and grab all of its subfolders
            StorageFolder assetsFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
            StorageFolderQueryResult results = assetsFolder.CreateFolderQuery();
            IReadOnlyList<StorageFolder> folders = await results.GetFoldersAsync();

            // create a query to only grab the filters folder
            StorageFolder filterFolder = folders.Where(fol => fol.DisplayName == "Filters").First();
            filters = await filterFolder.GetFilesAsync(); // read all files in the filter folder

            Debug.WriteLine("Displaying Folder");
            Debug.WriteLine(filterFolder.DisplayName);

            Debug.WriteLine("Displaying filter names");

            foreach (StorageFile fil in filters)
            {
                Debug.WriteLine(fil.DisplayName);
            }

            // if filters isn't null and we have at least one, a filter can be applied
            if (filters != null && filters.Count != 0)
                canAddFilter = true;

        }

        /// <summary>
        /// Show a filter that can be applied to the image
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void ShowFilters(object sender, RoutedEventArgs e)
        {
            // return if the user shouldn't be able to add filters
            if (canAddFilter == false || filters.Count == 0)
                return;

            // create a new UI element and set some style attributes for it
            Image filter = new Image();
            filter.VerticalAlignment = VerticalAlignment.Center;
            filter.HorizontalAlignment = HorizontalAlignment.Center;

            BitmapImage bm = null;

            // shows the first filter
            if (currentFilter == null)
            {
                // set bitmap to the first filter
                bm = new BitmapImage(new Uri(filters[0].Path));
                //filter.Source = bm;
            }
            else
            {
                // re-apply the bitmap of the current filter
                bm = new BitmapImage(new Uri(filters[currentFilterIndex].Path));
            }

            // set the source for the UI element and add the filter to the UI
            filter.Source = bm;
            currentFilter = filter;
            Canvas.SetZIndex(filter, 1);
            this.renderTarget.Children.Add(filter);
        }

        /// <summary>
        /// Manually stop the stream
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private async void StopStream(object sender, RoutedEventArgs e)
        {
            await CleanUp();
        }

        /// <summary>
        /// Manually start the stream, set the handler for suspending the application
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void StartStream(object sender, RoutedEventArgs e)
        {
            SetupCamera();
            Application.Current.Suspending += Current_Suspending;
        }

        /// <summary>
        /// Move the filter up
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void MoveFilterUp(object sender, RoutedEventArgs e)
        {
            // return if there is no filter to move
            if (currentFilter == null)
                return;

            // use the margin to move the filter up vertically
            //Image filter = currentFilter;
            Thickness distance = new Thickness(currentFilter.Margin.Left,
                                               currentFilter.Margin.Top - 5,
                                               currentFilter.Margin.Right,
                                               currentFilter.Margin.Bottom + 5);
            currentFilter.Margin = distance;
        }

        /// <summary>
        /// Move the filter down 
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void MoveFilterDown(object sender, RoutedEventArgs e)
        {
            // return if there is not filter to move
            if (currentFilter == null)
                return;

            // use the margin to move the filter down vertically
            //Image filter = currentFilter;
            Thickness distance = new Thickness(currentFilter.Margin.Left,
                                               currentFilter.Margin.Top + 5,
                                               currentFilter.Margin.Right,
                                               currentFilter.Margin.Bottom - 5);
            currentFilter.Margin = distance;
        }

        /// <summary>
        /// Move the filter to the left
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void MoveFilterLeft(object sender, RoutedEventArgs e)
        {
            // return if there is no filter to move
            if (currentFilter == null)
                return;

            // use the margin to move the filter to the left
            //Image filter = currentFilter;
            Thickness distance = new Thickness(currentFilter.Margin.Left - 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right + 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = distance;
        }

        /// <summary>
        /// Move the filter to the right
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void MoveFilterRight(object sender, RoutedEventArgs e)
        {
            // return if there is not filter to move
            if (currentFilter == null)
                return;

            // use the margin to move the filter to the right
            //Image filter = currentFilter;
            Thickness distance = new Thickness(currentFilter.Margin.Left + 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right - 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = distance;
        }

        /// <summary>
        /// Increase the overall size of the filter
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void IncreaseFilterScale(object sender, RoutedEventArgs e)
        {

            // return if there is no filter to scale
            if (currentFilter == null)
                return;

            // use the margin to scale the image
            //Image filter = currentFilter;
            Thickness scale = new Thickness(currentFilter.Margin.Left - 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right - 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = scale;
        }

        /// <summary>
        /// Decrease the overall size of the filter
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void DecreaseFilterScale(object sender, RoutedEventArgs e)
        {
            
            // return if there is no filter to scale
            if (currentFilter == null)
                return;

            // use the margin to scale the image
            //Image filter = currentFilter;
            Thickness scale = new Thickness(currentFilter.Margin.Left + 5,
                                               currentFilter.Margin.Top,
                                               currentFilter.Margin.Right + 5,
                                               currentFilter.Margin.Bottom);
            currentFilter.Margin = scale;
        }

        /// <summary>
        /// Rotate the filter to the left
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void RotateFilterLeft(object sender, RoutedEventArgs e)
        {
            return;
            // use rotate transfom
        }

        /// <summary>
        /// Rotate the filter to the right
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void RotateFilterRight(object sender, RoutedEventArgs e)
        {
            //return;
            // use rotate transfom
            // rotate the filter, then reset the center position of the transform
            currentFilter.RenderTransform = new RotateTransform();

            RotateTransform rotateTransform = currentFilter.RenderTransform as RotateTransform;

            if (rotateTransform == null)
                currentFilter.RenderTransform = new RotateTransform();


            return;
            
        }

        /// <summary>
        /// Save the image as a png file
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private async void SaveImage(object sender, RoutedEventArgs e)
        {
            // return if the user hasn't taken a picture
            if (imageCaptured == false)
                return;

            // don't allow the use to add any more filters to this image
            canAddFilter = false;

            // location:
            // C:\Users\{USER}\AppData\Local\Packages\7042ff83-dc0c-4816-a1a0-107ef134b815_zx9gxz867859y\LocalState
            StorageFolder assetsFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFile file = null;

            try
            {
                // create the image file and overwrite the last image that was saved
                file = await assetsFolder.CreateFileAsync("captured-photo.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            // remove the filter that hasn't been applied yet
            this.renderTarget.Children.Remove(currentFilter);
            currentFilter = null;

            // create a bitmap of the UI elements in the target grid
            RenderTargetBitmap targetBitmap = new RenderTargetBitmap();
            await targetBitmap.RenderAsync(this.renderTarget);

            // release the camera and remove all elements currently in the target view
            await CleanUp();
            ClearView();

            // show the image that is being saved
            this.renderedImage.Source = targetBitmap;
            Image newImage = new Image();
            newImage.Source = targetBitmap;
            this.renderTarget.Children.Add(newImage);

            capturedImage.Source = targetBitmap;

            if (file != null)
            {
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // setting flags, encoding, and the bitmap for the image to save
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    IBuffer targetBuffer = await targetBitmap.GetPixelsAsync();
                    SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, targetBitmap.PixelWidth, targetBitmap.PixelHeight);
                    softwareBitmap.CopyFromBuffer(targetBuffer);
                    encoder.SetSoftwareBitmap(softwareBitmap);

                    try
                    {
                        // flushing the bitmap out to the image file
                        await encoder.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }

        }

        /// <summary>
        /// Remove all UI elements currently in the target grid
        /// </summary>
        private void ClearView()
        {
            // Go through each element in the target grid and remove it
            // but keep the element that shows the camera stream
            foreach (UIElement child in this.renderTarget.Children)
            {
                if (child != this.camView)
                    this.renderTarget.Children.Remove(child);
            }

            // make sure the current filter is removed
            this.renderTarget.Children.Remove(currentFilter);
            currentFilter = null;

        }

        /// <summary>
        /// Take the bitmap of the target grid and use that as the captured image
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private async void CaptureImage(object sender, RoutedEventArgs e)
        {
            // return if the camera isn't streaming
            if (camStream == null)
                return;

            // show a count down to the user
            await Task.Delay(500);
            this.messageText.Text = "3";
            await Task.Delay(1000);
            this.messageText.Text = "2";
            await Task.Delay(1000);
            this.messageText.Text = "1";
            await Task.Delay(1000);
            this.messageText.Text = "";

            // create a bitmap of the target grid
            RenderTargetBitmap targetBitmap = new RenderTargetBitmap();
            await targetBitmap.RenderAsync(this.renderTarget);

            await CleanUp(); // release the camera resources

            // show the captured image so filters can be applied
            this.renderedImage.Source = targetBitmap;
            Image newImage = new Image();
            newImage.Source = targetBitmap;
            this.renderTarget.Children.Add(newImage);

            Rect rect = new Rect();
            rect = this.renderTarget.RenderTransform.TransformBounds(new Rect(this.renderTarget.RenderTransformOrigin, this.renderTarget.RenderSize));
            RectangleGeometry geo = new RectangleGeometry();
            geo.Rect = rect;

            this.renderTarget.Clip = geo;

            // let the user apply filters
            canAddFilter = true;
            imageCaptured = true;

        }

        /// <summary>
        /// Show the next available filter
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void GetNextFilter(object sender, RoutedEventArgs e)
        {
            // return if there isn't any filters or if we're at the last available one
            if (filters == null || filters.Count == 0)
                return;
            
            
            if (currentFilterIndex != filters.Count - 1)
            {
                // make sue we remove the current filter before replacing it
                if (currentFilter != null)
                    this.renderTarget.Children.Remove(currentFilter);

                currentFilterIndex++;

                // create and add style attributes to the new filter
                Image filter = new Image();
                filter.VerticalAlignment = VerticalAlignment.Center;
                filter.HorizontalAlignment = HorizontalAlignment.Center;

                BitmapImage bm = new BitmapImage(new Uri(filters[currentFilterIndex].Path));
                filter.Source = bm;

                Canvas.SetZIndex(filter, 1);
                this.renderTarget.Children.Add(filter);

                currentFilter = filter;

            }

        }

        /// <summary>
        /// Get the previous filter
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private void GetPrevFilter(object sender, RoutedEventArgs e)
        {
            // return if there isn't any filters or if we're at the first available
            if (filters == null || filters.Count == 0)
                return;
            
            if (currentFilterIndex != 0)
            {
                // make sure we remove the current filter
                if (currentFilter != null)
                    this.renderTarget.Children.Remove(currentFilter);

                currentFilterIndex--;

                // create and add style attributes to the new filter
                Image filter = new Image();
                filter.VerticalAlignment = VerticalAlignment.Center;
                filter.HorizontalAlignment = HorizontalAlignment.Center;

                BitmapImage bm = new BitmapImage(new Uri(filters[currentFilterIndex].Path));
                filter.Source = bm;

                Canvas.SetZIndex(filter, 1);
                this.renderTarget.Children.Add(filter);

                currentFilter = filter;

            }
        }

        /// <summary>
        /// Initiate the printing sequence 
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the route</param>
        private async void PrintImage(object sender, RoutedEventArgs e)
        {
            /*PrintDocument printDoc = new PrintDocument();
            IPrintDocumentSource printDocSource = printDoc.DocumentSource;

            PrintManager printMan = PrintManager.GetForCurrentView();*/

            // return if there isn't an image to print
            if (capturedImage == null)
                return;

            // register to print if needed
            if(isRegistered == false)
                RegisterForPrinting();

            await PrintManager.ShowPrintUIAsync(); // show the print UI

        }

        /// <summary>
        /// Add pages that will be sent to the printer
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments for added pages</param>
        private void PrintDocAddPages(object sender, AddPagesEventArgs e)
        {
            // add the captured image to the list of pages to print
            //printDoc.AddPage(capturedImage);
            //printDoc.AddPage(this.renderTarget);

            // let the application know that we've added all pages that will be printed
            ((PrintDocument)sender).AddPagesComplete();
        }

        /// <summary>
        /// Create the preview pages and set how the printed image will be styled
        /// </summary>
        /// <param name="sender">The application</param>
        /// <param name="e">Arguments from the print UI</param>
        private void PrintDocPaginate(object sender, PaginateEventArgs e)
        {
            // options to set different style attributes for the page to print
            PrintTaskOptions printOptions = ((PrintTaskOptions)e.PrintTaskOptions);
            printOptions.ColorMode = PrintColorMode.Color;  // allow color printing
            //printOptions.MediaType = PrintMediaType.Photographic; // print as a picture, not as a regular page
            printOptions.MediaType = PrintMediaType.Default;

            // get any options set by the user in the print UI
            PrintPageDescription pageDescription = printOptions.GetPageDescription(0);

            //printDoc.SetPreviewPage(1, capturedImage); // show a preview of the page to print
        }

        /// <summary>
        /// Handle requests to print from the application
        /// </summary>
        /// <param name="sender">Print manager for the window</param>
        /// <param name="e">Arguments for the requested print</param>
        private void PrintManPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs e)
        {
            PrintTask printTask = null;

            // handlers for the print UI
            printTask = e.Request.CreatePrintTask("Picture A-State", sourceRequested => 
            {
                printTask.Completed += (s, args) =>
                {
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                        Debug.WriteLine("Unable to print");
                    }

                    if (args.Completion == PrintTaskCompletion.Canceled || args.Completion == PrintTaskCompletion.Abandoned)
                    {
                        Debug.WriteLine("Print Task canceled or abandoned");
                    }

                    if (args.Completion == PrintTaskCompletion.Submitted)
                    {
                        Debug.WriteLine("Print Submitted");
                    }
                };

                // set the document source to print 
                sourceRequested.SetSource(printDocSource);

            });

            // turn off the preview of the pages that will be printed
            printTask.IsPreviewEnabled = false;
        }
    }
}

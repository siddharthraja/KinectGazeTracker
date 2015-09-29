// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.HDFaceBasics
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Media3D;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Face;
    using System.Drawing.Drawing2D;
    using System.Media;
    using System.IO;
    //using Microsoft.DirectX;

    /// <summary>
    /// Main Window
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Currently used KinectSensor
        /// </summary>
        /// 
        //Microsoft.DirectX.Quaternion quat;

        private KinectSensor sensor = null;

        /// <summary>
        /// Body frame source to get a BodyFrameReader
        /// </summary>
        private BodyFrameSource bodySource = null;

        /// <summary>
        /// Body frame reader to get body frames
        /// </summary>
        private BodyFrameReader bodyReader = null;

        /// <summary>
        /// HighDefinitionFaceFrameSource to get a reader and a builder from.
        /// Also to set the currently tracked user id to get High Definition Face Frames of
        /// </summary>
        private HighDefinitionFaceFrameSource highDefinitionFaceFrameSource = null;

        /// <summary>
        /// HighDefinitionFaceFrameReader to read HighDefinitionFaceFrame to get FaceAlignment
        /// </summary>
        private HighDefinitionFaceFrameReader highDefinitionFaceFrameReader = null;

        /// <summary>
        /// FaceAlignment is the result of tracking a face, it has face animations location and orientation
        /// </summary>
        private FaceAlignment currentFaceAlignment = null;

        /// <summary>
        /// FaceModel is a result of capturing a face
        /// </summary>
        private FaceModel currentFaceModel = null;

        /// <summary>
        /// FaceModelBuilder is used to produce a FaceModel
        /// </summary>
        private FaceModelBuilder faceModelBuilder = null;

        /// <summary>
        /// The currently tracked body
        /// </summary>
        private Body currentTrackedBody = null;

        /// <summary>
        /// The currently tracked body
        /// </summary>
        private ulong currentTrackingId = 0;

        /// <summary>
        /// Gets or sets the current tracked user id
        /// </summary>
        private string currentBuilderStatus = string.Empty;

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        private string statusText = "Ready To Start Capture";

        private static Vector4 basePoint;
        SoundPlayer player = new SoundPlayer();
        double timer1=(DateTime.Now - DateTime.Today).TotalMilliseconds, timer2=-1;

        private bool cmdGiven = false, mirrorChecked=false, cmdDone=true;
        int Count = 1;
        
        double currentLeft, currentRight;
        string[] cmds = { "left", "right", "center" };
        
        StreamWriter outputFile;
        double TS_start, TS_issued, TS_chk, TS_done;
        int errors=0;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.DataContext = this;
            TS_start = (DateTime.Now - DateTime.Today).TotalMilliseconds;

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the current tracked user id
        /// </summary>
        private ulong CurrentTrackingId
        {
            get
            {
                return this.currentTrackingId;
            }

            set
            {
                this.currentTrackingId = value;

                this.StatusText = this.MakeStatusText();
            }
        }

        /// <summary>
        /// Gets or sets the current Face Builder instructions to user
        /// </summary>
        private string CurrentBuilderStatus
        {
            get
            {
                return this.currentBuilderStatus;
            }

            set
            {
                this.currentBuilderStatus = value;

                this.StatusText = this.MakeStatusText();
            }
        }

        /// <summary>
        /// Called when disposed of
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose based on whether or not managed or native resources should be freed
        /// </summary>
        /// <param name="disposing">Set to true to free both native and managed resources, false otherwise</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.currentFaceModel != null)
                {
                    this.currentFaceModel.Dispose();
                    this.currentFaceModel = null;
                }
            }
        }

        /// <summary>
        /// Returns the length of a vector from origin
        /// </summary>
        /// <param name="point">Point in space to find it's distance from origin</param>
        /// <returns>Distance from origin</returns>
        private static double VectorLength(CameraSpacePoint point)
        {
            //Console.Write("CameraSpacePoint: " + point.X + " | " + point.Y + " | " + point.Z + " | " + "\n");
            
            //Console.Write("VectorLength: " + point.X + "\n");
            var result = Math.Pow(point.X, 2) + Math.Pow(point.Y, 2) + Math.Pow(point.Z, 2);
            basePoint.X = point.X;
            basePoint.Y = point.Y;
            basePoint.Z = point.Z;

            result = Math.Sqrt(result);

            return result;
        }

        /// <summary>
        /// Finds the closest body from the sensor if any
        /// </summary>
        /// <param name="bodyFrame">A body frame</param>
        /// <returns>Closest body, null of none</returns>
        private static Body FindClosestBody(BodyFrame bodyFrame)
        {
            Body result = null;
            double closestBodyDistance = double.MaxValue;
            
            Body[] bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);

            foreach (var body in bodies)
            {
                if (body.IsTracked)
                {
                    var currentLocation = body.Joints[JointType.SpineBase].Position;

                    var currentDistance = VectorLength(currentLocation);

                    if (result == null || currentDistance < closestBodyDistance)
                    {
                        result = body;
                        closestBodyDistance = currentDistance;
                        //Console.Write("closestBodyDistance " + closestBodyDistance + "\n");
            
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Find if there is a body tracked with the given trackingId
        /// </summary>
        /// <param name="bodyFrame">A body frame</param>
        /// <param name="trackingId">The tracking Id</param>
        /// <returns>The body object, null of none</returns>
        private static Body FindBodyWithTrackingId(BodyFrame bodyFrame, ulong trackingId)
        {
            Body result = null;

            Body[] bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);

            foreach (var body in bodies)
            {
                if (body.IsTracked)
                {
                    if (body.TrackingId == trackingId)
                    {
                        result = body;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the current collection status
        /// </summary>
        /// <param name="status">Status value</param>
        /// <returns>Status value as text</returns>
        private static string GetCollectionStatusText(FaceModelBuilderCollectionStatus status)
        {
            string res = string.Empty;

            if ((status & FaceModelBuilderCollectionStatus.FrontViewFramesNeeded) != 0)
            {
                res = "FrontViewFramesNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.LeftViewsNeeded) != 0)
            {
                res = "LeftViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.RightViewsNeeded) != 0)
            {
                res = "RightViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.TiltedUpViewsNeeded) != 0)
            {
                res = "TiltedUpViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.Complete) != 0)
            {
                res = "Complete";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.MoreFramesNeeded) != 0)
            {
                res = "TiltedUpViewsNeeded-More";
                return res;
            }

            return res;
        }

        /// <summary>
        /// Helper function to format a status message
        /// </summary>
        /// <returns>Status text</returns>
        private string MakeStatusText()
        {
            string status = string.Format(System.Globalization.CultureInfo.CurrentCulture, "Builder Status: {0}, Current Tracking ID: {1}", this.CurrentBuilderStatus, this.CurrentTrackingId);
            
            return status;
        }

        /// <summary>
        /// Fires when Window is Loaded
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeHDFace();
        }

        /// <summary>
        /// Initialize Kinect object
        /// </summary>
        private void InitializeHDFace()
        {
            
            this.CurrentBuilderStatus = "Ready To Start Capture";

            this.sensor = KinectSensor.GetDefault();
            this.bodySource = this.sensor.BodyFrameSource;
            this.bodyReader = this.bodySource.OpenReader();
            this.bodyReader.FrameArrived += this.BodyReader_FrameArrived;

            this.highDefinitionFaceFrameSource = new HighDefinitionFaceFrameSource(this.sensor);
            this.highDefinitionFaceFrameSource.TrackingIdLost += this.HdFaceSource_TrackingIdLost;

            this.highDefinitionFaceFrameReader = this.highDefinitionFaceFrameSource.OpenReader();
            this.highDefinitionFaceFrameReader.FrameArrived += this.HdFaceReader_FrameArrived;

            this.currentFaceModel = new FaceModel();
            this.currentFaceAlignment = new FaceAlignment();

            this.InitializeMesh();
            this.UpdateMesh();

            this.sensor.Open();
            Console.Write("\n\n******************************************************\n" + "Command, Issued_TS, Checked_TS, Done_TS, Errors\n");
            
        }

        /// <summary>
        /// Initializes a 3D mesh to deform every frame
        /// </summary>
        
        private void InitializeMesh(){}
       
        /// <summary>
        /// Sends the new deformed mesh to be drawn
        /// </summary>
        
        private void UpdateMesh(){}
        
        /// <summary>
        /// Start a face capture on clicking the button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void StartCapture_Button_Click(object sender, RoutedEventArgs e)
        {
            this.StartCapture();
        }

        /// <summary>
        /// This event fires when a BodyFrame is ready for consumption
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            this.CheckOnBuilderStatus();

            var frameReference = e.FrameReference;
            using (var frame = frameReference.AcquireFrame())
            {
                if (frame == null)
                {
                    // We might miss the chance to acquire the frame, it will be null if it's missed
                    return;
                }

                if (this.currentTrackedBody != null)
                {
                    this.currentTrackedBody = FindBodyWithTrackingId(frame, this.CurrentTrackingId);

                    if (this.currentTrackedBody != null)
                    {
                        return;
                    }
                }

                Body selectedBody = FindClosestBody(frame);

                if (selectedBody == null)
                {
                    return;
                }

                this.currentTrackedBody = selectedBody;
                this.CurrentTrackingId = selectedBody.TrackingId;

                this.highDefinitionFaceFrameSource.TrackingId = this.CurrentTrackingId;
            }
        }
        
        /// <summary>
        /// This event is fired when a tracking is lost for a body tracked by HDFace Tracker
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void HdFaceSource_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            var lostTrackingID = e.TrackingId;

            if (this.CurrentTrackingId == lostTrackingID)
            {
                this.CurrentTrackingId = 0;
                this.currentTrackedBody = null;
                if (this.faceModelBuilder != null)
                {
                    this.faceModelBuilder.Dispose();
                    this.faceModelBuilder = null;
                }

                this.highDefinitionFaceFrameSource.TrackingId = 0;
            }
        }

        /******************************************
         * ****************************************
         * ***************************************/
        private void issueCommand()
        {
            Random r = new Random();
            //Count = ++Count % 3;
            Count = r.Next(0, 3);
            txt_cmd.Text = cmds[Count];
            player.Play();
            cmdGiven = true;
            mirrorChecked = cmdDone = false;
            TS_issued = (DateTime.Now - DateTime.Today).TotalMilliseconds;
            errors = 0;
        }


        /// <summary>
        /// This event is fired when a new HDFace frame is ready for consumption
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void HdFaceReader_FrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs e)
        {
            
            using (var frame = e.FrameReference.AcquireFrame())
            {
                // We might miss the chance to acquire the frame; it will be null if it's missed.
                // Also ignore this frame if face tracking failed.
                if (frame == null || !frame.IsFaceTracked)
                {
                    return;
                }
                Microsoft.Kinect.Vector4 orientation = currentFaceAlignment.FaceOrientation;
                changePoint(CalculateGazePoint(orientation));

                if(cmdDone){
                    issueCommand();
                }
                if (cmdGiven && !mirrorChecked) {
                    string s = checkMirrorGaze(currentLeft, currentRight);
                    if (cmds[Count] == s)
                    {
                        mirrorChecked = true;
                        TS_chk = (DateTime.Now - DateTime.Today).TotalMilliseconds;
                        TS_chk = TS_chk - TS_issued;
                        txt_cmd.Text = "YOU GOT IT!!!";
                        player.Play();
            
                    }
                    else
                        if (s != "none") errors++;
                    
                }
                else
                {
                    if (currentLeft >= 322 && currentLeft <= 422 && currentRight >= 280 && currentRight <= 380)
                    {
                        cmdDone = true;
                        cmdGiven = false;
                        TS_done = (DateTime.Now - DateTime.Today).TotalMilliseconds;
                        TS_done = TS_done - TS_issued;
                        //outputFile.WriteLine(cmds[Count] + ", " + TS_issued + ", " + TS_chk + ", " + TS_done);
                        Console.Write(cmds[Count] + ", " + (int)(TS_issued - TS_start) + ", " + (int)TS_chk + ", " + (int)TS_done + ", " + errors + "\n");
                    }
                }
                
            
                //Console.Write("Orientation: " + orientation.X + "\n");
                frame.GetAndRefreshFaceAlignmentResult(this.currentFaceAlignment);
                this.UpdateMesh();
            }
        }

        private Vector4 CalculateGazePoint(Vector4 v)
        {
            float x=0, y=0, z=1;    // N

            float qx = v.X;
            float qy = v.Y;
            float qz = v.Z;
            float qw = v.W;

            float ix = qw * x + qy * z - qz * y;
            float iy = qw * y + qz * x - qx * z;
            float iz = qw * z + qx * y - qy * x;
            float iw = -qx * x - qy * y - qz * z;

            Vector4 ray = new Vector4(); // V
            ray.X = ix * qw + iw * -qx + iy * -qz - iz * -qy;
            ray.Y = iy * qw + iw * -qy + iz * -qx - ix * -qz;
            ray.Z = iz * qw + iw * -qz + ix * -qy - iy * -qx;
            
            //t = -(Po * N + d) / (V * N)
            //P = Po + tV

            Vector4 PoN = new Vector4(); // Po * N
            PoN.X = basePoint.X * x;
            PoN.Y = basePoint.Y * y;
            PoN.Z = basePoint.Z * z;

            Vector4 VN = new Vector4(); // V * N
            VN.X = ray.X * x;
            VN.Y = ray.Y * y;
            VN.Z = ray.Z * z;
            
            float t = (PoN.X + PoN.Y + PoN.Z + basePoint.Z) / (VN.X + VN.Y + VN.Z);

            Vector4 P = new Vector4();
            P.X = basePoint.X + (t * ray.X);
            P.Y = basePoint.Y + (t * ray.Y);
            P.Z = basePoint.Z + (t * ray.Z);

            //Console.Write(P.X + " | " + P.Y + " | " + P.Z + "\n");
            return P;
        }

        private void changePoint(Vector4 p)
        {
            // 743----361.5    695---337.5
            double x = -p.X;
            double y = p.Y;
            x *= 125;
            y *= 125;
            //keepGazeTrack(checkMirrorGaze(361.5 + x, 337.5 + y));
            currentLeft = 361.5 + x;
            currentRight = 340.5 + y;  //337.5 + y;
            //Console.Write("\n" + currentLeft + " | " + currentRight);
            System.Windows.Controls.Canvas.SetLeft(trackDot, currentLeft);
            System.Windows.Controls.Canvas.SetTop(trackDot, currentRight);
            
        }

        private string checkMirrorGaze(double x, double y) 
        {
            if ((129 <= x) && x <= (129 + 100) && y >= 278 && y <= (278 + 100)) // left mirror
            {
                timer2 = (DateTime.Now - DateTime.Today).TotalMilliseconds;
                return "left";
            }
            if ((586 <= x) && x <= (586 + 100) && y >= 278 && y <= (278 + 100)) // right mirror
            {
                timer2 = (DateTime.Now - DateTime.Today).TotalMilliseconds;
                return "right";
            }
            if ((460 <= x) && x <= (460 + 70) && y >= 190 && y <= (190 + 100)) // top mirror
            {
                timer2 = (DateTime.Now - DateTime.Today).TotalMilliseconds;
                return "center";
            }
            
            return "none";
        }

        /*
        private void keepGazeTrack(bool looking){
            Console.Write(looking+"\n");
                
            if (!looking)
            {
                if ((timer2 - timer1 > 1000))
                {
                    player.SoundLocation = soundDirectory + "sounds\\alert.wav";
                    player.Play();
                }
            }
            else
                timer1 = timer2;
        }
        */
        /// <summary>
        /// Start a face capture operation
        /// </summary>
        private void StartCapture()
        {
            this.StopFaceCapture();

            this.faceModelBuilder = null;

            this.faceModelBuilder = this.highDefinitionFaceFrameSource.OpenModelBuilder(FaceModelBuilderAttributes.None);

            this.faceModelBuilder.BeginFaceDataCollection();

            this.faceModelBuilder.CollectionCompleted += this.HdFaceBuilder_CollectionCompleted;
        }

        /// <summary>
        /// Cancel the current face capture operation
        /// </summary>
        private void StopFaceCapture()
        {
            if (this.faceModelBuilder != null)
            {
                this.faceModelBuilder.Dispose();
                this.faceModelBuilder = null;
            }
        }

        /// <summary>
        /// This event fires when the face capture operation is completed
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void HdFaceBuilder_CollectionCompleted(object sender, FaceModelBuilderCollectionCompletedEventArgs e)
        {
            var modelData = e.ModelData;

            this.currentFaceModel = modelData.ProduceFaceModel();

            this.faceModelBuilder.Dispose();
            this.faceModelBuilder = null;

            this.CurrentBuilderStatus = "Capture Complete";
        }

        /// <summary>
        /// Check the face model builder status
        /// </summary>
        private void CheckOnBuilderStatus()
        {
            if (this.faceModelBuilder == null)
            {
                return;
            }

            string newStatus = string.Empty;

            var captureStatus = this.faceModelBuilder.CaptureStatus;
            newStatus += captureStatus.ToString();

            var collectionStatus = this.faceModelBuilder.CollectionStatus;

            newStatus += ", " + GetCollectionStatusText(collectionStatus);

            this.CurrentBuilderStatus = newStatus;
        }
    }
}
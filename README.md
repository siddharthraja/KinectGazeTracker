# KinectHeadTrack
This code is written for a head tracking application with the Kinect v2. The program follows the head movements of the person and 
gives visual feedback to the user by showing a tracking dot on the screen.

To sync up with a Kinect v2, you need a Windows 8 PC with DirectX11 support and USB 3.0 ports.

To get started also download the required sdks from Microsoft's website and install Visual Studio 2013. What I used was Visual Studio 2013 for Windows Desktop.

The GazeTrack.csproj is how you load the project in Visual Studio. MainWindow.xaml and MainWindow.xaml.cs are the main files to get things started. This C# file handles the scene and is the center piece for the entire program.

The code is fairly straightforward to understand in terms of logic. The main piece is the calculation of the vectors and the 
quarternions that give the orientation of the plane of the face.


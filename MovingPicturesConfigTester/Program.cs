using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Plugins.MovingPictures;

namespace MovingPicturesConfigTester {
    class Program {

        [STAThreadAttribute]
        static void Main(string[] args) {
            MovingPicturesPlugin plugin = new MovingPicturesPlugin();
            plugin.ShowPlugin();
        }
    }
}
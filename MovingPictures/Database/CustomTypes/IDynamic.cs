using System;
using System.Collections.Generic;
using System.Text;

namespace MediaPortal.Plugins.MovingPictures.Database.CustomTypes {
    // A delegate type for hooking up change notifications.
    public delegate void ChangedEventHandler(object sender, EventArgs e);

    interface IDynamic {
        // An event that clients can use to be notified whenever the
        // elements of the list change.
        event ChangedEventHandler Changed;
    }
}
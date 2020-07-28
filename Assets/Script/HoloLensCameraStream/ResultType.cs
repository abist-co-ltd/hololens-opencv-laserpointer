//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

namespace HoloLensCameraStream
{
    /// <summary>
    /// Represents the reason why the callback fired.
    /// </summary>
    public enum ResultType
    {
        /// <summary>
        /// Everything went okay, continue down the happy path.
        /// </summary>
        Success,
        /// <summary>
        /// A function was called when the VideoCapture object when in the wrong state.
        /// For instance, alling StopVideoModeAsync() when video mode is already stopped
        /// will result in an early calling of the callback as the video mode does not need
        /// time to be stopped.
        /// </summary>
        InappropriateState,

        /// <summary>
        /// Something went wrong when performing the async operation.
        /// VideoCapture should not be considered a stable, usable object.
        /// </summary>
        UnknownError
    }
}

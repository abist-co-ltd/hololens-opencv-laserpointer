//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

namespace HoloLensCameraStream
{
    public struct VideoCaptureResult
    {
        /// <summary>
        /// Not really used. Set to 1 when success=false, and is 0 when success=true
        /// </summary>
        public readonly long hResult;

        /// <summary>
        /// Represents the reason why the callback fired.
        /// </summary>
        public readonly ResultType resultType;

        /// <summary>
        /// A simple answer of whether or not everything worked out with the async process.
        /// </summary>
        public readonly bool success;

        internal VideoCaptureResult(long hResult, ResultType resultType, bool success)
        {
            this.hResult = hResult;
            this.resultType = resultType;
            this.success = success;
        }
    }
}

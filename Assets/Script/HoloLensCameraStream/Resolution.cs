//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

namespace HoloLensCameraStream
{
    /// <summary>
    /// A structure for holding a resolution.
    /// </summary>
    public struct Resolution
    {
        /// <summary>
        /// The width property.
        /// </summary>
        public readonly int width;

        /// <summary>
        /// The height property.
        /// </summary>
        public readonly int height;

        public Resolution(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }
}

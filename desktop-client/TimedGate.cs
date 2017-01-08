/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;

namespace ShowdownSoftware
{
    public class TimedGate
    {
        private DateTime then;
        
        public TimeSpan Interval { get; set; }
        public bool IsRunning { get; set; }

        public TimedGate(TimeSpan interval, bool start = true)
        {
            Interval = interval;
            if(start) Start();
        }
        
        public TimedGate(int milliseconds, bool start = true)
            : this(TimeSpan.FromMilliseconds(milliseconds)) { }

        public TimedGate(double seconds, bool start = true)
            : this(TimeSpan.FromSeconds(seconds)) { }
        
        public void Start()
        {
            if(!IsRunning)
            {
                IsRunning = true;
                then = DateTime.Now;
            }
        }

        public void Stop()
        {
            if(IsRunning)
                IsRunning = false;
        }

        public bool TryEnter()
        {
            if(IsRunning)
            {
                var now = DateTime.Now;
                if(now - then >= Interval) {
                    then = now;
                    return true;
                }
            }

            return false;
        }
    }
}

﻿using System;

namespace homeControl.ClientApi.Server
{
    internal interface IClientProcessor: IDisposable
    {
        void Start();
        void Stop();
        event EventHandler Disconnected;
    }
}
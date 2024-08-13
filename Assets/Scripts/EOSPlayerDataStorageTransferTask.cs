/*
* Copyright (c) 2024 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

namespace PlayEveryWare.EpicOnlineServices.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Epic.OnlineServices;
    using Epic.OnlineServices.PlayerDataStorage;
    using UnityEngine;

    public class EOSPlayerDataStorageTransferTask : EOSTransferTask
    {
        /// <summary>
        /// Holder for the EOS SDK's transfer information about the current request.
        /// When the file data transfer begins, this value will then be set.
        /// If the task hits an early exception, this task may be completed without this getting set.
        /// </summary>
        public PlayerDataStorageFileTransferRequest TransferRequest;

        public EOSPlayerDataStorageTransferTask(TaskCompletionSource<byte[]> innerTask) : base(innerTask)
        {
        }

        public EOSPlayerDataStorageTransferTask(TaskCompletionSource<byte[]> innerTask, byte[] data) : base(innerTask, data)
        {
        }

        public EOSPlayerDataStorageTransferTask(Result resultCode, Exception failureException = null) : base(resultCode, failureException)
        {
        }
    }
}
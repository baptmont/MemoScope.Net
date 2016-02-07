﻿using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using MemoScope.Core;
using MemoScope.Modules.Explorer;
using Microsoft.Diagnostics.Runtime;
using WinFwk.UIMessages;
using WinFwk.UIServices;
using WinFwk.UIModules;
using System.Threading;

namespace MemoScope.Services
{
    public class DumpLoaderService : AbstractUIService, IMessageListener<OpenDumpRequest>
    {
        private readonly TaskFactory fact;

        public DumpLoaderService()
        {
            var sched = new LimitedConcurrencyLevelTaskScheduler(1);
            fact = new TaskFactory(sched);
        }

        public void HandleMessage(OpenDumpRequest openDumpRequest)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            var token = source.Token;
            foreach (var fileInfo in openDumpRequest.FileInfos)
            {
                fact.StartNew(() =>
                {
                    BeginTask("Loading file: " + fileInfo.FullName, source);
                    DataTarget target = DataTarget.LoadCrashDump(fileInfo.FullName);
                    try
                    {
                        if( Environment.Is64BitProcess && target.PointerSize != 8 )
                        { 
                            throw new InvalidOperationException($"Wrong architecture ! Dumpfile : {target.PointerSize*8} bits, Environment.Is64BitProcess : {Environment.Is64BitProcess}");
                        }

                        var clrDump = new ClrDump(target, fileInfo.FullName, MessageBus);
                        clrDump.InitCache(token);
                        if (token.IsCancellationRequested)
                        {
                            clrDump.Destroy();
                            EndTask("File NOT loaded: " + fileInfo.FullName);
                        }
                        else
                        {
                            MessageBus.SendMessage(new ClrDumpLoadedMessage(clrDump));
                            EndTask("File loaded: " + fileInfo.FullName);
                        }
                    }
                    catch
                    {
                        target.Dispose();
                        throw;
                    }
                });
            }
        }
    }
}
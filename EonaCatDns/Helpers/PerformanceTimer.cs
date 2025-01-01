/*
EonaCatDns
Copyright (C) 2017-2025 EonaCat (Jeroen Saey)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License

*/

using System;
using System.Diagnostics;
using EonaCat.Logger;

namespace EonaCat.Dns.Helpers;

public class PerformanceTimer : IDisposable
{
    private readonly string _message;
    private readonly Stopwatch _timer;

    public PerformanceTimer(string message = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            message = "";
        }

        _message = message;
        Logger.LogAsync($"{_message} performanceTimer started", ELogType.DEBUG).ConfigureAwait(false);

        _timer = new Stopwatch();
        _timer.Start();
    }

    public async void Dispose()
    {
        _timer.Stop();
        var milliSeconds = _timer.ElapsedMilliseconds;
        await Logger.LogAsync($"{_message} - Elapsed: {MilliSecondsToReadableFormat(milliSeconds)}", ELogType.DEBUG)
            .ConfigureAwait(false);
        await Logger.LogAsync($"{_message} performanceTimer stopped", ELogType.DEBUG).ConfigureAwait(false);
    }

    public static string MilliSecondsToReadableFormat(long milliSeconds)
    {
        var timeSpan = TimeSpan.FromMilliseconds(milliSeconds);
        var result = $"{timeSpan.Hours:D2}h:{timeSpan.Minutes:D2}m:{timeSpan.Seconds:D2}s:{timeSpan.Milliseconds:D3}";
        return result;
    }
}
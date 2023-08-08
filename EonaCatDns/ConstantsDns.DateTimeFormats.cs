/*
EonaCatDns
Copyright (C) 2017-2023 EonaCat (Jeroen Saey)

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

namespace EonaCat.Dns;

public static partial class ConstantsDns
{
    public static class DateTimeFormats
    {
        public static string DateTimeDayStats => "dd-MM HH";

        public static string DateTimeWeekStats => "dd-MM HH";
        public static string DateTimeHourStats => "HH:mm";

        public static string DateTimeYearStats => "dd-MM-yyyy";

        public static string DateTimeMonthStats => "dd-MM HH";

        public static string ClientDate => "yyyy-MM-dd HH:mm";
    }
}
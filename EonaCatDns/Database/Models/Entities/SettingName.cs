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

namespace EonaCat.Dns.Database.Models.Entities;

public static class SettingName
{
    public static string Internet => nameof(Internet);
    public static string WebservicePort => nameof(WebservicePort);
    public static string Blockedaddress => nameof(Blockedaddress);

    public static string ClientsBorderColor => nameof(ClientsBorderColor);
    public static string QueryType1Color => nameof(QueryType1Color);
    public static string QueryType2Color => nameof(QueryType2Color);
    public static string QueryType3Color => nameof(QueryType3Color);
    public static string QueryType4Color => nameof(QueryType4Color);
    public static string QueryType5Color => nameof(QueryType5Color);
    public static string QueryType6Color => nameof(QueryType6Color);
    public static string QueryType7Color => nameof(QueryType7Color);
    public static string QueryType8Color => nameof(QueryType8Color);
    public static string QueryType9Color => nameof(QueryType9Color);
    public static string QueryType10Color => nameof(QueryType10Color);

    public static string ClientsBackgroundColor => nameof(ClientsBackgroundColor);

    public static string BlockedBorderColor => nameof(BlockedBorderColor);

    public static string BlockedBackgroundColor => nameof(BlockedBackgroundColor);

    public static string AllowedBackgroundColor => nameof(AllowedBackgroundColor);

    public static string RefusedBorderColor => nameof(RefusedBorderColor);

    public static string RefusedBackgroundColor => nameof(RefusedBackgroundColor);

    public static string NameErrorBorderColor => nameof(NameErrorBorderColor);

    public static string NameErrorBackgroundcolor => nameof(NameErrorBackgroundcolor);

    public static string ServerFailureBorderColor => nameof(ServerFailureBorderColor);

    public static string ServerFailureBackgroundColor => nameof(ServerFailureBackgroundColor);

    public static string CachedBorderColor => nameof(CachedBorderColor);

    public static string CachedBackgroundColor => nameof(CachedBackgroundColor);

    public static string NoErrorBorderColor => nameof(NoErrorBorderColor);

    public static string NoErrorBackgroundColor => nameof(NoErrorBackgroundColor);

    public static string TotalQueriesBorderColor => nameof(TotalQueriesBorderColor);

    public static string TotalQueriesBackgroundColor => nameof(TotalQueriesBackgroundColor);
}
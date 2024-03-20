namespace PlayEveryWare.EpicOnlineServices.Samples
{
    using Epic.OnlineServices.Auth;
    using System;

    public static class LoginCredentialTypeExtensions
    {
        public static string GetLabel(this LoginCredentialType type)
        {
            return type switch
            {
                LoginCredentialType.Password       => "Password",
                LoginCredentialType.ExchangeCode   => "Exchange Code",
                LoginCredentialType.PersistentAuth => "Persistent Auth",
                LoginCredentialType.DeviceCode     => "Device Code",
                LoginCredentialType.Developer      => "Developer",
                LoginCredentialType.RefreshToken   => "Refresh Token",
                LoginCredentialType.AccountPortal  => "Account Portal",
                LoginCredentialType.ExternalAuth   => "External Auth",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static string ToString(this LoginCredentialType type)
        {
            return type.GetLabel();
        }
    }
}
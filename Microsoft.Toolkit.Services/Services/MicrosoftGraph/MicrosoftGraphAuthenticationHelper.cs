// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
#if WINRT || WINDOWS_UWP
using System.Net.Http;
#endif
using System.Threading.Tasks;
using Microsoft.Identity.Client;
#if WINRT || WINDOWS_UWP
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Windows.Storage;
#endif
using MSAL = Microsoft.Identity.Client;

namespace Microsoft.Toolkit.Services.MicrosoftGraph
{
    /// <summary>
    /// Authentication Helper Using Azure Active Directory v2.0 app Model
    /// </summary>
    public class MicrosoftGraphAuthenticationHelper
    {
        /// <summary>
        /// Base Url for service.
        /// </summary>
        protected const string Authority = "https://login.microsoftonline.com/common/";

        /// <summary>
        /// Default Redirect Uri
        /// </summary>
        protected const string DefaultRedirectUri = "urn:ietf:wg:oauth:2.0:oob";

        /// <summary>
        /// Default Authority url for V2
        /// </summary>
        protected const string AuthorityV2Model = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

        /// <summary>
        /// Default Authorization Token Service
        /// </summary>
        protected const string AuthorizationTokenService = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

        /// <summary>
        /// Default Logout Url for V2
        /// </summary>
        protected const string LogoutUrlV2Model = "https://login.microsoftonline.com/common/oauth2/v2.0/logout";

#if WINRT || WINDOWS_UWP || HAS_UNO
        private const string LogoutUrl = "https://login.microsoftonline.com/common/oauth2/logout";
        private const string MicrosoftGraphResource = "https://graph.microsoft.com";

        /// <summary>
        /// Storage key name for user name.
        /// </summary>
        private static readonly string STORAGEKEYUSER = "user";

#endif

        private static MSAL.PublicClientApplication _identityClient = null;

#if WINRT || WINDOWS_UWP || HAS_UNO
        /// <summary>
        /// Password vault used to store access tokens
        /// </summary>
        private readonly Windows.Security.Credentials.PasswordVault _vault;

        /// <summary>
        /// Azure Active Directory Authentication context use to get an access token [ADAL]
        /// </summary>
        private AuthenticationContext _azureAdContext = new AuthenticationContext(Authority);
#endif

        /// <summary>
        /// Gets or sets delegated permission Scopes
        /// </summary>
        protected string[] DelegatedPermissionScopes { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftGraphAuthenticationHelper"/> class.
        /// </summary>
        public MicrosoftGraphAuthenticationHelper()
        {
#if WINRT || WINDOWS_UWP || HAS_UNO
            _vault = new Windows.Security.Credentials.PasswordVault();
#endif
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftGraphAuthenticationHelper"/> class.
        /// </summary>
        /// <param name="delegatedPermissionScopes">Delegated Permission Scopes</param>
        public MicrosoftGraphAuthenticationHelper(string[] delegatedPermissionScopes)
            : this()
        {
            DelegatedPermissionScopes = delegatedPermissionScopes;
        }

        /// <summary>
        /// Gets or sets the Oauth2 access token.
        /// </summary>
        protected string TokenForUser { get; set; }

        /// <summary>
        /// Gets or sets token expiration.  By default the life time of the first access token is 3600 (1h).
        /// </summary>
        public DateTimeOffset Expiration { get; set; }

        /// <summary>
        /// Clean the TokenCache
        /// </summary>
        internal void CleanToken()
        {
            TokenForUser = null;
#if WINRT || WINDOWS_UWP || HAS_UNO
            _azureAdContext.TokenCache.Clear();
#endif
        }

        /// <summary>
        /// Get a Microsoft Graph access token using the v2.0 Endpoint
        /// </summary>
        /// <param name="appClientId">Application client Id</param>
        /// <param name="loginHint">UPN</param>
        /// <returns>An oauth2 access token.</returns>
        public async Task<string> GetUserTokenV2Async(string appClientId, string loginHint)
        {
            return await GetUserTokenV2Async(appClientId, null, null, loginHint);
        }

        /// <summary>
        /// Get a Microsoft Graph access token using the v2.0 Endpoint.
        /// </summary>
        /// <param name="appClientId">Application client ID</param>
        /// <param name="uiParent">UiParent instance - required for Android</param>
        /// <param name="redirectUri">Redirect Uri - required for Android</param>
        /// <param name="loginHint">UPN</param>
        /// <returns>An oauth2 access token.</returns>
        public async Task<string> GetUserTokenV2Async(string appClientId, UIParent uiParent = null, string redirectUri = null, string loginHint = null)
        {
            if (_identityClient == null)
            {
                _identityClient = new MSAL.PublicClientApplication(appClientId);
            }

            if (!string.IsNullOrEmpty(redirectUri))
            {
                _identityClient.RedirectUri = redirectUri;
            }

            var upnLoginHint = string.Empty;
            if (!string.IsNullOrEmpty(loginHint))
            {
                upnLoginHint = loginHint;
            }

            MSAL.AuthenticationResult authenticationResult = null;

            try
            {
                IAccount account = (await _identityClient.GetAccountsAsync()).FirstOrDefault();
                authenticationResult = await _identityClient.AcquireTokenSilentAsync(DelegatedPermissionScopes, account);
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    authenticationResult = await _identityClient.AcquireTokenAsync(DelegatedPermissionScopes, upnLoginHint, uiParent);
                }
                catch (MsalException)
                {
                    throw;
                }
            }

            return authenticationResult?.AccessToken;
        }

        /// <summary>
        /// Logout the user
        /// </summary>
        /// <returns>Success or failure</returns>
        [Obsolete("This method will be removed, please use LogoutAsync instead.")]
        public bool Logout()
        {
            return LogoutV2();
        }

        /// <summary>
        /// Logout the user using the V2 endpoint
        /// </summary>
        /// <returns>Success or failure</returns>
        [Obsolete("This method will be removed, please use LogoutAsync instead.")]
        public bool LogoutV2()
        {
            Task<bool> task = Task.Run<bool>(async () => await LogoutAsync());
            return task.Result;
        }

        /// <summary>
        /// Logout the user using the V2 endpoint asynchronous
        /// </summary>
        /// <returns>Success or failure</returns>
        public async Task<bool> LogoutAsync()
        {
            try
            {
                IAccount account = (await _identityClient.GetAccountsAsync()).FirstOrDefault();
                await _identityClient.RemoveAsync(account);
            }
            catch (MsalException)
            {
                return false;
            }

            return true;
        }

#if WINRT || WINDOWS_UWP || HAS_UNO
        /// <summary>
        /// Get a Microsoft Graph access token from Azure AD.
        /// </summary>
        /// <param name="appClientId">Azure AD application client ID</param>
        /// <param name="resourceId">Azure AD application resource ID</param>
        /// <param name="promptBehavior">Prompt behavior</param>
        /// <returns>An oauth2 access token.</returns>
        public async Task<string> GetUserTokenAsync(string appClientId, string resourceId = MicrosoftGraphResource, PromptBehavior promptBehavior = PromptBehavior.Always)
        {
            // For the first use get an access token prompting the user, after one hour
            // refresh silently the token
            if (TokenForUser == null)
            {
                IdentityModel.Clients.ActiveDirectory.AuthenticationResult userAuthnResult = await _azureAdContext.AcquireTokenAsync(
					resourceId,
					appClientId,
					new Uri(DefaultRedirectUri),
#if __IOS__
					new IdentityModel.Clients.ActiveDirectory.PlatformParameters(Windows.UI.Xaml.Application.Current.Window.RootViewController, false)
#elif NETFX_CORE
					new IdentityModel.Clients.ActiveDirectory.PlatformParameters(PromptBehavior.Always, false)
#elif __ANDROID__
					new IdentityModel.Clients.ActiveDirectory.PlatformParameters(/*UNO TODO */ null, false)
#else
					new IdentityModel.Clients.ActiveDirectory.PlatformParameters()
#endif
				);
                TokenForUser = userAuthnResult.AccessToken;
                Expiration = userAuthnResult.ExpiresOn;
            }

            if (Expiration <= DateTimeOffset.UtcNow.AddMinutes(5))
            {
                IdentityModel.Clients.ActiveDirectory.AuthenticationResult userAuthnResult = await _azureAdContext.AcquireTokenSilentAsync(resourceId, appClientId);
                TokenForUser = userAuthnResult.AccessToken;
                Expiration = userAuthnResult.ExpiresOn;
            }

            return TokenForUser;
        }

        /// <summary>
        /// Logout the user
        /// </summary>
        /// <param name="authenticationModel">Authentication version endPoint</param>
        /// <returns>Success or failure</returns>
        public async Task<bool> LogoutAsync(string authenticationModel)
        {
            HttpResponseMessage response = null;
            ApplicationData.Current.LocalSettings.Values[STORAGEKEYUSER] = null;

            if (authenticationModel.Equals("V1"))
            {
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, LogoutUrl);
                    response = await client.SendAsync(request);
                    return response.IsSuccessStatusCode;
                }
            }

            if (authenticationModel.Equals("V2"))
            {
                return await LogoutAsync();
            }

            return true;
        }
#endif
			}

#if HAS_UNO
	//
	// Summary:
	//     Indicates whether AcquireToken should automatically prompt only if necessary
	//     or whether it should prompt regardless of whether there is a cached token.
	public enum PromptBehavior
	{
		//
		// Summary:
		//     Acquire token will prompt the user for credentials only when necessary. If a
		//     token that meets the requirements is already cached then the user will not be
		//     prompted.
		Auto = 0,
		//
		// Summary:
		//     The user will be prompted for credentials even if there is a token that meets
		//     the requirements already in the cache.
		Always = 1,
		//
		// Summary:
		//     The user will not be prompted for credentials. If prompting is necessary then
		//     the AcquireToken request will fail.
		Never = 2,
		//
		// Summary:
		//     Re-authorizes (through displaying webview) the resource usage, making sure that
		//     the resulting access token contains updated claims. If user logon cookies are
		//     available, the user will not be asked for credentials again and the logon dialog
		//     will dismiss automatically.
		RefreshSession = 3
	}
#endif
}

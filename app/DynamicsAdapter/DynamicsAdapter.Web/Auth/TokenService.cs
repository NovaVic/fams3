﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace DynamicsAdapter.Web.Auth
{
    public interface ITokenService
    {
        Task<Token> GetTokenAsync(CancellationToken cancellationToken);
    }

    public class TokenService : ITokenService
    {

        private readonly string token_key = "oauth-token";

        private readonly IOAuthApiClient _oAuthApiClient;
        private readonly IDistributedCache _distributedCache;

        public TokenService(IOAuthApiClient oAuthApiClient, IDistributedCache distributedCache)
        {
            _oAuthApiClient = oAuthApiClient;
            _distributedCache = distributedCache;
        }

        public async Task<Token> GetTokenAsync(CancellationToken cancellationToken)
        {
            return await GetOrRefreshTokenAsync(cancellationToken);
        }

        private async Task<Token> GetOrRefreshTokenAsync(CancellationToken cancellationToken)
        {
            var token = await GetFromCacheAsync();
            if (token == null)
                return await RefreshTokenAsync(cancellationToken);
            return token;
        }

        private async Task<Token> GetFromCacheAsync()
        {
            var tokenString = await _distributedCache.GetStringAsync(this.token_key);
            if (String.IsNullOrEmpty(tokenString)) return null;
            return JsonConvert.DeserializeObject<Token>(tokenString);
        }

        private async Task<Token> RefreshTokenAsync(CancellationToken cancellationToken)
        {
            var token = await _oAuthApiClient.GetRefreshToken(cancellationToken);
            await _distributedCache.SetStringAsync(this.token_key, JsonConvert.SerializeObject(token), new DistributedCacheEntryOptions() { AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, token.ExpiresIn - 10) }, cancellationToken);
            return token;
        }

    }
}
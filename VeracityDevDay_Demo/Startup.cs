using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.DependencyInjection;
using Stardust.Particles;
using System;
using Veracity.Common.OAuth;
using Veracity.Common.OAuth.Providers;
using Veracity.Services.Api.Extensions;
using IDataProtector = Veracity.Common.OAuth.IDataProtector;
using ILogger = Stardust.Interstellar.Rest.Common.ILogger;

namespace VeracityDevDay_Demo
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
                .AddAzureKeyVault("https://veracitydevdaydemo.vault.azure.net/", new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback)), new DefaultKeyVaultSecretManager())
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }
        
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddVeracity(Configuration)
                .AddScoped<IOAuthTokenProvider, TokenProvider>()
                .AddSingleton<TokenProviderConfiguration, TokenProviderConfiguration>()
                .AddHttpContextAccessor()
                .AddSingleton<ILogger, LogWrapper>()
                .AddSingleton<ILogging, LogWrapper>()
                .AddScoped(s => s.GetService<IHttpContextAccessor>().HttpContext.User)
                .AddSingleton(ConstructDataProtector)
                .AddSingleton(ConstructDistributedCache)
                .AddDistributedRedisCache(opt =>
                    {
                        opt.Configuration = Configuration.GetSection("Veracity").GetValue<string>("RedisConnectionString");
                        opt.InstanceName = "master";
                    })
                .AddScoped<TokenCacheBase, DistributedTokenCache>()
                .AddVeracityServices(ConfigurationManagerHelper.GetValueOnKey("myApiV3Url"))
                .AddAuthentication(sharedOptions =>
                {
                    sharedOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddVeracityAuthentication(Configuration)
                .AddCookie();

            services.AddMvc()
                .AddVeracityApiProxies(ConfigurationManagerHelper.GetValueOnKey("myApiV3Url"), CookieAuthenticationDefaults.AuthenticationScheme);

        }
        private IDistributedCache ConstructDistributedCache(IServiceProvider s)
        {
            return s.GetService<IDistributedCache>();
            //return new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
        }

        private IDataProtector ConstructDataProtector(IServiceProvider s)
        {
            return new DataProtector<IDataProtectionProvider>(s.GetDataProtectionProvider(), (p, data) => p.CreateProtector("token").Protect(data), (p, data) => p.CreateProtector("token").Unprotect(data));
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseCookiePolicy();

            app.UseStaticFiles()
                .UseVeracity()
                .UseAuthentication()
                .UseMvc();
        }
    }
}

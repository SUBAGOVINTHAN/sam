using SamErpBackend.Models;

namespace SamErpBackend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettingsModel>(Configuration.GetSection("AppSettings"));
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(60);
                options.Cookie.HttpOnly = true;
                options.Cookie.Name = "sam_erp_session";

                // FIX 1: Use SameSiteMode.Lax (not None) for HTTP dev.
                // SameSite=None requires Secure=true, which breaks over plain HTTP.
                options.Cookie.SameSite = SameSiteMode.Lax;

                // FIX 2: SameAsRequest lets the cookie work over both HTTP (dev)
                // and HTTPS (prod), instead of Always which blocks HTTP entirely.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // CORS — allow the React dev server
            services.AddCors(options =>
            {
                options.AddPolicy("ReactPolicy", policy =>
                    policy.WithOrigins("http://localhost:8001",
				"http://122.165.126.163:8001",
				"http://base.kalanjiyam.info:8001")
                          .AllowCredentials()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
                // FIX 3: No UseHttpsRedirection() in dev.
                // It silently redirects HTTP → HTTPS, causing ERR_EMPTY_RESPONSE
                // when the frontend calls http://localhost:PORT.
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
                app.UseHttpsRedirection(); // Only redirect to HTTPS in production
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("ReactPolicy");   // Must be after UseRouting, before UseSession

            app.UseSession();

            // FIX 4: Authentication must come BEFORE Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
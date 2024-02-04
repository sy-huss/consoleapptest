using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Diagnostics;

namespace WebApiExample
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
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Deployment API", Version = "v1" });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Deployment API v1"));

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class DeploymentController : ControllerBase
    {
        [HttpPost]
        [Route("deploy")]
        public async Task<IActionResult> Deploy([FromBody] DeploymentRequest request)
        {
            // Validate the request
            if (string.IsNullOrEmpty(request.GitRepositoryUrl) || string.IsNullOrEmpty(request.ProjectPath))
            {
                return BadRequest("GitRepositoryUrl and ProjectPath are required.");
            }

            // Clone the Git repository
            string repoPath = "/tmp/repo-" + Guid.NewGuid(); // Ensuring a unique directory
            string gitCloneCommand = $"git clone {request.GitRepositoryUrl} {repoPath}";
            await ExecuteCommand(gitCloneCommand);

            // Navigate to the project directory (if specified)
            string projectDirectoryPath = Path.Combine(repoPath, request.ProjectPath);

            // Run dotnet commands
            string dotnetRestoreCommand = $"dotnet restore {projectDirectoryPath}";
            await ExecuteCommand(dotnetRestoreCommand, projectDirectoryPath);

            string dotnetBuildCommand = $"dotnet build {projectDirectoryPath}";
            await ExecuteCommand(dotnetBuildCommand, projectDirectoryPath);

            string dotnetRunCommand = $"dotnet run --project {projectDirectoryPath}";
            await ExecuteCommand(dotnetRunCommand, projectDirectoryPath);

            // Clean up if needed Consider whether you want to delete the repository after use

            return Ok("Deployment process completed successfully.");
        }

        private async Task ExecuteCommand(string command, string workingDirectory = null)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"{command}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }

                process.Start();
                string result = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                // Log or handle the output as needed
                Console.WriteLine(result);
            }
        }

        public class DeploymentRequest
        {
            public string GitRepositoryUrl { get; set; }
            public string ProjectPath { get; set; } // Relative path within the repository
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
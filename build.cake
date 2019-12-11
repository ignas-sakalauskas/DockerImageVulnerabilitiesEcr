//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=Microsoft.Data.Tools.Msbuild&version=10.0.61804.210"

//////////////////////////////////////////////////////////////////////
// ADDINS
//////////////////////////////////////////////////////////////////////
#addin "nuget:?package=Cake.Docker&version=0.10.1"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");

var region = EnvironmentVariable("AWS_REGION") ?? throw new ArgumentException("AWS_REGION environment variable is null or empty.");
var profile = EnvironmentVariable("AWS_PROFILE") ?? throw new ArgumentException("AWS_PROFILE environment variable is null or empty.");
var ecr = EnvironmentVariable("ECS_REPOSITORY") ?? throw new ArgumentException("ECS_REPOSITORY environment variable is null or empty.");

(string Name, string Path)[] projects;
(string Name, string Tag)[] dockerFiles;

Setup(context =>
{
    dockerFiles = new [] 
    {
        ("DockerfileDebian", "debian"),
        ("DockerfileUbuntu", "ubuntu"),
        ("DockerfileAlpine", "alpine"),
    };

    projects = new []
    {
        ("helloworld21", "./src/HelloWorld2.1"),
        ("helloworld22", "./src/HelloWorld2.2"),        
        ("helloworld30", "./src/HelloWorld3.0"),        
        ("helloworld31", "./src/HelloWorld3.1"),        
    };
});

Task("__DockerBuild")
    .Does(() =>
    {
        foreach (var project in projects)
        {
            foreach (var dockerFile in dockerFiles)
            {
                var settings = new DockerImageBuildSettings
                {
                    File = $"{project.Path}/{dockerFile.Name}",
                    Pull = true, // pull the latest image
                    Tag = new[] 
                    {
                        $"{ecr}:{project.Name}-{dockerFile.Tag}"
                    }
                };

                DockerBuild(settings, ".");
            }
        }
    });

Task("__DockerLogin")
    .Does(() =>
    {
        var args = $"ecr get-login --no-include-email --region {region} --profile {profile}";
        Information($"Docker login arguments: {args}");

        var getLoginSettings = new ProcessSettings
        {
            Arguments = args,
            RedirectStandardOutput = true
        };

        string dockerLoginCmd;
        using(var process = StartAndReturnProcess("aws", getLoginSettings))
        {
            dockerLoginCmd = process.GetStandardOutput().ElementAt(0);
            process.WaitForExit();
        }

        var loginSettings = new ProcessSettings
        {
            Arguments = dockerLoginCmd.Replace("docker", "")
        };

        StartProcess("docker", loginSettings);
    });

Task("__DockerPush")
    .Does(() =>
    {
        foreach (var project in projects)
        {
            foreach (var dockerFile in dockerFiles)
            {
                DockerPush($"{ecr}:{project.Name}-{dockerFile.Tag}");
            }
        }
    });

Task("Build")
    .IsDependentOn("__DockerBuild")
    .IsDependentOn("__DockerLogin")
    .IsDependentOn("__DockerPush");

RunTarget(target);

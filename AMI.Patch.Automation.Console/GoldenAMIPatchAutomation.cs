using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AMI.Patch.Automation.Console.Models;

namespace AMI.Patch.Automation.Console
{
    public class GoldenAMIPatchAutomation : IGoldenAMIPatchAutomation
    {
        public async Task Execute(BaseInputModel baseInput, GoldenAMIPatchModel goldenAMIPatch)
        {
            string accessKey = baseInput.AccessKey;
            string secretKey = baseInput.SecretKey;
            string region = baseInput.Region;

            string autoScalingGroupName = goldenAMIPatch.AutoScalingGroupName;
            int desiredCapacity = goldenAMIPatch.DesiredCapacity;
            string patchDocumentName = goldenAMIPatch.PatchDocumentName;
            string goldenImageAmiId = goldenAMIPatch.GoldenImageAmiId;
            string launchConfigurationName = goldenAMIPatch.LaunchConfigurationName;

            // Step 0: Configure AWS credentials
            AWSConfigs.AWSProfileName = "default";
            //AWSConfigs.AWSAccessKeyId = accessKey;
            //AWSConfigs.AWSSecretAccessKey = secretKey;
            AWSConfigs.RegionEndpoint = RegionEndpoint.GetBySystemName(region);

            // Initialize clients
            var autoScalingClient = new AmazonAutoScalingClient();
            var ssmClient = new AmazonSimpleSystemsManagementClient();

            // Initialize EC2 client
            var ec2Client = new AmazonEC2Client();

            // Step 1: Launch a new EC2 instance from the Golden Image AMI
            string newInstanceInstanceId = await LaunchEC2InstanceAsync(ec2Client, goldenImageAmiId);

            // Step 2: Create a Patch Document
            await CreatePatchDocumentAsync(ssmClient, patchDocumentName);

            // Step 3: Connect to the instance and apply patch updates using SSM
            await ApplyPatchUpdatesWithSSMAsync(ssmClient, newInstanceInstanceId);

            var newInstanceInstanceIds = new List<string>() { newInstanceInstanceId };
            // Step 4: Monitor Patching Progress
            await MonitorPatchingProgress(ssmClient, patchDocumentName, newInstanceInstanceIds);

            // Step 4.1: Patch Application on New Instances
            // Connect to the new instance using SSH/RDP and verify the patch application manually

            // Step 5: Run tests on the updated instance
            await RunTestsOnInstanceAsync(ec2Client, newInstanceInstanceId);

            // Step 6: Create a new AMI from the updated instance
            string newAmiId = await CreateAMIAsync(ec2Client, newInstanceInstanceId);

            // Step 7: Update Auto Scaling Launch Configuration to use the new AMI
            await CreateLaunchConfigurationAsync(autoScalingClient, launchConfigurationName, newAmiId);

            // Step 8: Update the desired capacity of the Auto Scaling Group
            await UpdateDesiredCapacity(autoScalingClient, autoScalingGroupName, desiredCapacity);

            // Step 9: Wait for New Instances to Become Healthy
            await WaitForHealthyInstances(autoScalingClient, autoScalingGroupName, desiredCapacity);

            // Step 10: 
            List<string> newInstanceIds = await GetInstanceIdsFromAutoScalingGroupAsync(autoScalingClient, autoScalingGroupName);

            System.Console.WriteLine("Patching completed.");

            // Step 11: Test the New Instances
            bool testsPass = await PerformTestsAsync(newInstanceIds); // Replace with your actual test logic

            if (!testsPass)
            {
                System.Console.WriteLine("Tests failed. Leaving new instances up.");
            }
            else
            {
                // Step 12: Update Desired Capacity to Replace Old Instances
                await UpdateAutoScalingGroupCapacityAsync(autoScalingClient, autoScalingGroupName, desiredCapacity);

                // Step 13: Monitor Replacement Progress
                await MonitorReplacementProgressAsync(autoScalingClient, autoScalingGroupName, desiredCapacity);
                System.Console.WriteLine("Instance replacement completed.");
            }
        }

        //Step 1
        async Task<string> LaunchEC2InstanceAsync(AmazonEC2Client ec2Client, string amiId)
        {
            var request = new RunInstancesRequest
            {
                ImageId = amiId,
                InstanceType = "t2.micro",
                MinCount = 1,
                MaxCount = 1,
                KeyName = "my-key-pair" // Replace with your key pair name
            };

            var runInstancesResponse = await ec2Client.RunInstancesAsync(request);
            System.Console.WriteLine($"runInstancesResponse HttpStatusCode: {runInstancesResponse.HttpStatusCode}");

            var instanceId = runInstancesResponse.Reservation.Instances[0].InstanceId;
            System.Console.WriteLine($"EC2 instance launched with ID: {instanceId}");

            return instanceId;
        }

        //Step 2
        private async Task CreatePatchDocumentAsync(AmazonSimpleSystemsManagementClient ssmClient, string patchDocumentName)
        {
            string patchDocumentContent = @"
            {
                ""schemaVersion"": ""2.2"",
                ""description"": ""Sample Patch Document"",
                ""mainSteps"": [
                    {
                        ""action"": ""aws:runShellScript"",
                        ""name"": ""runShellScript"",
                        ""inputs"": {
                            ""runCommand"": [""yum update -y""]
                        }
                    }
                ]
            }";
            var request = new CreateDocumentRequest
            {
                Name = patchDocumentName,
                DocumentType = DocumentType.Command,
                DocumentFormat = DocumentFormat.JSON,
                Content = patchDocumentContent
            };
            var createDocumentResponse = await ssmClient.CreateDocumentAsync(request);
            System.Console.WriteLine($"createDocumentResponse HttpStatusCode: {createDocumentResponse.HttpStatusCode}");

            System.Console.WriteLine("Patch document created.");
        }

        //Step 3
        async Task ApplyPatchUpdatesWithSSMAsync(AmazonSimpleSystemsManagementClient ssmClient, string instanceId)
        {
            var runCommandRequest = new SendCommandRequest
            {
                InstanceIds = new List<string> { instanceId },
                DocumentName = "AWS-RunPatchBaseline", // Replace with the appropriate SSM document name for patching
                TimeoutSeconds = 3600 // Timeout for command execution in seconds
            };

            var sendCommandResponse = await ssmClient.SendCommandAsync(runCommandRequest);
            System.Console.WriteLine($"sendCommandResponse HttpStatusCode: {sendCommandResponse.HttpStatusCode}");

            var commandId = sendCommandResponse.Command.CommandId;

            System.Console.WriteLine($"Patch update command sent with Command ID: {commandId}");

            // Monitor command status
            await MonitorCommandStatusAsync(ssmClient, commandId);
        }

        async Task MonitorCommandStatusAsync(AmazonSimpleSystemsManagementClient ssmClient, string commandId)
        {
            while (true)
            {
                var getCommandInvocationRequest = new GetCommandInvocationRequest
                {
                    CommandId = commandId,
                    InstanceId = "INSTANCE_ID" // Replace with the actual instance ID
                };

                var getCommandInvocationResponse = await ssmClient.GetCommandInvocationAsync(getCommandInvocationRequest);
                System.Console.WriteLine($"getCommandInvocationResponse HttpStatusCode: {getCommandInvocationResponse.HttpStatusCode}");

                var status = getCommandInvocationResponse.Status;

                System.Console.WriteLine($"Command Status: {status}");

                if (status == CommandInvocationStatus.Success ||
                    status == CommandInvocationStatus.Failed ||
                    status == CommandInvocationStatus.Cancelled)
                {
                    break;
                }

                // Wait before checking status again
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        //Step 4
        private async Task MonitorPatchingProgress(AmazonSimpleSystemsManagementClient ssmClient, string patchDocumentName, List<string> newInstanceIds)
        {
            bool allComplete = false;
            while (!allComplete)
            {
                allComplete = true;
                foreach (var instanceId in newInstanceIds)
                {
                    var request = new ListCommandInvocationsRequest
                    {
                        InstanceId = instanceId,
                        CommandId = patchDocumentName
                    };
                    var listCommandInvocationsResponse = await ssmClient.ListCommandInvocationsAsync(request);
                    System.Console.WriteLine($"listCommandInvocationsResponse HttpStatusCode: {listCommandInvocationsResponse.HttpStatusCode}");

                    if (listCommandInvocationsResponse.CommandInvocations.Count == 0 || listCommandInvocationsResponse.CommandInvocations.Any(invocation => invocation.Status != "Success"))
                    {
                        allComplete = false;
                        break;
                    }
                }
                if (!allComplete)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10)); // Wait for 10 seconds before checking again
                }
            }
            System.Console.WriteLine("Patching completed.");
        }

        //Step 5
        async Task RunTestsOnInstanceAsync(AmazonEC2Client ec2Client, string instanceId)
        {
            // Perform your tests here on the instance
            // This could involve running commands, scripts, or interacting with the instance

            System.Console.WriteLine("Running tests on the updated instance...");
            // Simulating a delay to represent running tests
            await Task.Delay(TimeSpan.FromSeconds(30));
        }

        //Step 6
        async Task<string> CreateAMIAsync(AmazonEC2Client ec2Client, string instanceId)
        {
            var createImageRequest = new CreateImageRequest
            {
                InstanceId = instanceId,
                Name = "MyUpdatedInstanceAMI",
                Description = "AMI created from an updated instance"
            };

            var createImageResponse = await ec2Client.CreateImageAsync(createImageRequest);
            System.Console.WriteLine($"createImageResponse HttpStatusCode: {createImageResponse.HttpStatusCode}");

            return createImageResponse.ImageId;
        }

        //Step 7
        private async Task CreateLaunchConfigurationAsync(AmazonAutoScalingClient autoScalingClient, string launchConfigurationName,
            string newAmiId)
        {
            // Get the current launch configuration
            var describeRequest = new DescribeLaunchConfigurationsRequest
            {
                LaunchConfigurationNames = new List<string> { launchConfigurationName }
            };
            var describeLaunchConfigurationsResponse = await autoScalingClient.DescribeLaunchConfigurationsAsync(describeRequest);
            System.Console.WriteLine($"describeLaunchConfigurationsResponse HttpStatusCode: {describeLaunchConfigurationsResponse.HttpStatusCode}");

            var currentLaunchConfiguration = describeLaunchConfigurationsResponse.LaunchConfigurations.FirstOrDefault();

            if (currentLaunchConfiguration == null)
            {
                System.Console.WriteLine("Launch Configuration not found.");
                return;
            }

            // Create a new launch configuration based on the current one with the new AMI
            var newLaunchConfigurationName = $"{launchConfigurationName}-Updated";
            var createRequest = new CreateLaunchConfigurationRequest
            {
                LaunchConfigurationName = newLaunchConfigurationName,
                ImageId = newAmiId,
                InstanceType = currentLaunchConfiguration.InstanceType,
                // Set other properties as needed
            };
            var createLaunchConfigurationResponse = await autoScalingClient.CreateLaunchConfigurationAsync(createRequest);
            System.Console.WriteLine($"createLaunchConfigurationResponse HttpStatusCode: {createLaunchConfigurationResponse.HttpStatusCode}");

            System.Console.WriteLine($"New Launch Configuration created: {newLaunchConfigurationName}");
        }

        //Step 8
        async Task UpdateDesiredCapacity(AmazonAutoScalingClient autoScalingClient, string autoScalingGroupName, int newDesiredCapacity)
        {
            var updateRequest = new UpdateAutoScalingGroupRequest
            {
                AutoScalingGroupName = autoScalingGroupName,
                DesiredCapacity = newDesiredCapacity,
                MinSize = newDesiredCapacity, // Ensure min size is adjusted accordingly
                MaxSize = newDesiredCapacity, // Ensure max size is adjusted accordingly
                NewInstancesProtectedFromScaleIn = false
            };

            var updateAutoScalingGroupResponse = await autoScalingClient.UpdateAutoScalingGroupAsync(updateRequest);
            System.Console.WriteLine($"updateAutoScalingGroupResponse HttpStatusCode: {updateAutoScalingGroupResponse.HttpStatusCode}");
        }

        //Step 9
        private async Task WaitForHealthyInstances(AmazonAutoScalingClient autoScalingClient, string autoScalingGroupName, int desiredCapacity)
        {
            var describeRequest = new DescribeAutoScalingGroupsRequest
            {
                AutoScalingGroupNames = new List<string> { autoScalingGroupName }
            };

            int healthyInstances = 0;

            while (healthyInstances < desiredCapacity)
            {
                var describeAutoScalingGroupsResponse = await autoScalingClient.DescribeAutoScalingGroupsAsync(describeRequest);
                System.Console.WriteLine($"describeAutoScalingGroupsResponse HttpStatusCode: {describeAutoScalingGroupsResponse.HttpStatusCode}");

                var group = describeAutoScalingGroupsResponse.AutoScalingGroups[0];

                healthyInstances = group.Instances.FindAll(instance => instance.HealthStatus == "Healthy").Count;

                System.Console.WriteLine($"{healthyInstances}/{desiredCapacity} instances are healthy...");

                // Sleep for a while before checking again
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            System.Console.WriteLine("All instances are healthy!");
        }


        //Step 10
        private async Task<List<string>> GetInstanceIdsFromAutoScalingGroupAsync(AmazonAutoScalingClient autoScalingClient, string autoScalingGroupName)
        {
            var describeAutoScalingGroupsResponse = await autoScalingClient.DescribeAutoScalingGroupsAsync(new DescribeAutoScalingGroupsRequest
            {
                AutoScalingGroupNames = new List<string> { autoScalingGroupName }
            });
            System.Console.WriteLine($"describeAutoScalingGroupsResponse HttpStatusCode: {describeAutoScalingGroupsResponse.HttpStatusCode}");
            return describeAutoScalingGroupsResponse.AutoScalingGroups[0].Instances.Select(instance => instance.InstanceId).ToList();
        }

        //Step 11
        private async Task<bool> PerformTestsAsync(List<string> newInstanceIds)
        {
            string accessKey = "YOUR_ACCESS_KEY";
            string secretKey = "YOUR_SECRET_KEY";
            string region = "us-west-2";

            AmazonEC2Client ec2Client = new AmazonEC2Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));

            foreach (var instanceId in newInstanceIds)
            {
                try
                {
                    // Replace with your actual test logic here
                    // For example, send a request to a specific endpoint on the instance
                    var request = new DescribeInstancesRequest
                    {
                        InstanceIds = new List<string> { instanceId }
                    };
                    var describeInstancesResponse = await ec2Client.DescribeInstancesAsync(request);
                    System.Console.WriteLine($"describeInstancesResponse HttpStatusCode: {describeInstancesResponse.HttpStatusCode}");

                    // Perform checks on the response to validate the test
                    // If the tests fail, return false
                    // Otherwise, continue to the next instance
                }
                catch (Exception ex)
                {
                    // Handle exceptions that might occur during testing
                    System.Console.WriteLine($"Error testing instance {instanceId}: {ex}");
                    return false;
                }
            }

            // All tests passed
            return true;
        }

        //Step 12
        private async Task UpdateAutoScalingGroupCapacityAsync(AmazonAutoScalingClient autoScalingClient, string autoScalingGroupName, int desiredCapacity)
        {
            var request = new UpdateAutoScalingGroupRequest
            {
                AutoScalingGroupName = autoScalingGroupName,
                DesiredCapacity = desiredCapacity
            };
            var updateAutoScalingGroupResponse = await autoScalingClient.UpdateAutoScalingGroupAsync(request);
            System.Console.WriteLine($"updateAutoScalingGroupResponse HttpStatusCode: {updateAutoScalingGroupResponse.HttpStatusCode}");

            System.Console.WriteLine("Auto Scaling Group desired capacity updated.");
        }

        //Step 13
        private async Task MonitorReplacementProgressAsync(AmazonAutoScalingClient autoScalingClient, string autoScalingGroupName, int desiredCapacity)
        {
            while (true)
            {
                var response = await autoScalingClient.DescribeAutoScalingGroupsAsync(new DescribeAutoScalingGroupsRequest
                {
                    AutoScalingGroupNames = new List<string> { autoScalingGroupName }
                });
                var instancesInService = response.AutoScalingGroups[0].Instances.Count(instance => instance.LifecycleState == "InService");
                if (instancesInService == desiredCapacity)
                {
                    break;
                }
                Thread.Sleep(TimeSpan.FromSeconds(10)); // Wait for 10 seconds before checking again
            }
            System.Console.WriteLine("Instance replacement completed.");
        }
    }
}

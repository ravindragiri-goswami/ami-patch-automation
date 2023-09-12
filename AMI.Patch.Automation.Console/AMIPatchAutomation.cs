using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using AMI.Patch.Automation.Console.Models;

namespace AMI.Patch.Automation.Console
{
    public class AMIPatchAutomation
    {
        public async Task Execute(BaseInputModel baseInput, AMIPatchModel amiPatch)
        {
            string InstanceId = amiPatch.InstanceId;
            string NewInstanceUserData = amiPatch.NewInstanceUserData;
            string NewInstanceType = amiPatch.NewInstanceType;
            string NewAMIName = amiPatch.NewAMIName;
            string NewAMIDescription = amiPatch.NewAMIDescription;

            var ec2Client = new AmazonEC2Client(RegionEndpoint.USWest2); // Replace with your desired AWS region

            // Step 1: Create an AMI from an existing instance
            var createImageResponse = await CreateAMIFromExistingInstance(InstanceId, NewAMIName, NewAMIDescription, ec2Client);

            // Step 2: Launch a new instance from the AMI and apply patches
            var newInstanceId = await LaunchNewInstanceFromAMIApplyPatches(createImageResponse,
                NewInstanceType, NewInstanceUserData, ec2Client);

            // Add code here to wait for the new instance to be ready (e.g., using DescribeInstances)

            // Step 3: Test the new instance
            // Add code here to perform automated testing on the new instance
            WaitForInstanceToBeReady(ec2Client, newInstanceId);

            // Step 4: Create a new AMI from the tested instance
            var createImageFromInstanceResponse = await CreateNewAMIFromTestedInstance(newInstanceId, ec2Client);

            // Step 5: Launch new instances with the final AMI
            // Add code here to launch instances with the new AMI as needed
            var launchNewInstancesResponse = await LaunchNewInstancesWithFinalAMI(createImageFromInstanceResponse,
                NewInstanceType, ec2Client);

            // Step 6: Test the new instances
            // Add code here to perform automated testing on the new instances
            TestNewInstances(launchNewInstancesResponse);

            List<string> oldInstanceIds = GetOldInstanceIds();

            // Step 7: Drain or keep old instances based on test results
            // Add code here to handle old instances based on test results
            KeepOldInstances(oldInstanceIds);

            System.Console.WriteLine("Automation completed successfully");
        }

        // Step 1: Create an AMI from an existing instance
        private async Task<CreateImageResponse> CreateAMIFromExistingInstance(string InstanceId, string NewAMIName, string NewAMIDescription,
            AmazonEC2Client ec2Client)
        {
            var createImageRequest = new CreateImageRequest
            {
                InstanceId = InstanceId,
                Name = NewAMIName,
                Description = NewAMIDescription
            };
            var createImageResponse = await ec2Client.CreateImageAsync(createImageRequest);

            System.Console.WriteLine($"Step 1: Created AMI {createImageResponse.ImageId}");

            return createImageResponse;
        }

        // Step 2: Launch a new instance from the AMI and apply patches
        private async Task<string> LaunchNewInstanceFromAMIApplyPatches(CreateImageResponse createImageResponse,
            string NewInstanceType, string NewInstanceUserData, AmazonEC2Client ec2Client)
        {
            var runInstancesRequest = new RunInstancesRequest
            {
                ImageId = createImageResponse.ImageId,
                InstanceType = NewInstanceType,
                UserData = NewInstanceUserData,
                MinCount = 1,
                MaxCount = 1,
                // Add other instance parameters as needed
            };
            var runInstancesResponse = await ec2Client.RunInstancesAsync(runInstancesRequest);

            var newInstanceId = runInstancesResponse.Reservation.Instances[0].InstanceId;
            System.Console.WriteLine($"Step 2: Launched new instance {newInstanceId} from the AMI");

            return newInstanceId;
        }

        // Step 3: Test the new instance
        // Add code here to perform automated testing on the new instance
        static async Task WaitForInstanceToBeReady(IAmazonEC2 ec2Client, string instanceId)
        {
            System.Console.WriteLine("Waiting for the new instance to be ready...");

            var describeInstancesRequest = new DescribeInstancesRequest
            {
                InstanceIds = new List<string> { instanceId }
            };

            while (true)
            {
                var describeInstancesResponse = await ec2Client.DescribeInstancesAsync(describeInstancesRequest);

                if (describeInstancesResponse.Reservations.Count > 0 &&
                    describeInstancesResponse.Reservations[0].Instances.Count > 0)
                {
                    var instance = describeInstancesResponse.Reservations[0].Instances[0];

                    if (instance.State.Name == InstanceStateName.Running)
                    {
                        System.Console.WriteLine("New instance is running and ready.");
                        break;
                    }
                }

                // Wait for a few seconds before checking again
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            System.Console.WriteLine("Step 3: Automated testing completed successfully");
        }

        // Step 4: Create a new AMI from the tested instance
        private async Task<CreateImageResponse> CreateNewAMIFromTestedInstance(string newInstanceId, AmazonEC2Client ec2Client)
        {
            var createImageFromInstanceRequest = new CreateImageRequest
            {
                InstanceId = newInstanceId,
                Name = "Final-AMI",
                Description = "Final AMI Description"
            };
            var createImageFromInstanceResponse = await ec2Client.CreateImageAsync(createImageFromInstanceRequest);

            System.Console.WriteLine($"Step 4: Created final AMI {createImageFromInstanceResponse.ImageId}");

            return createImageFromInstanceResponse;
        }

        // Step 5: Launch new instances with the final AMI
        // Add code here to launch instances with the new AMI as needed
        private async Task<RunInstancesResponse> LaunchNewInstancesWithFinalAMI(CreateImageResponse createImageFromInstanceResponse,
            string NewInstanceType, AmazonEC2Client ec2Client)
        {
            var finalAMIId = createImageFromInstanceResponse.ImageId; // Replace with the actual final AMI ID
            var numInstancesToLaunch = 2; // Change as needed

            var launchNewInstancesRequest = new RunInstancesRequest
            {
                ImageId = finalAMIId,
                InstanceType = NewInstanceType, // Use the desired instance type
                MinCount = numInstancesToLaunch,
                MaxCount = numInstancesToLaunch,
                // Add other instance parameters as needed
            };

            var launchNewInstancesResponse = await ec2Client.RunInstancesAsync(launchNewInstancesRequest);

            foreach (var newInstance in launchNewInstancesResponse.Reservation.Instances)
            {
                System.Console.WriteLine($"Launched new instance {newInstance.InstanceId} from the final AMI");
            }

            return launchNewInstancesResponse;
        }

        // Step 6: Test the new instances
        // Add code here to perform automated testing on the new instances
        private void TestNewInstances(RunInstancesResponse launchNewInstancesResponse)
        {
            foreach (var newInstance in launchNewInstancesResponse.Reservation.Instances)
            {
                System.Console.WriteLine($"Testing new instance {newInstance.InstanceId}...");

                // Replace this with your actual testing logic or scripts
                bool testResult = RunCustomTests(newInstance.InstanceId);

                if (testResult)
                {
                    System.Console.WriteLine($"Tests passed for instance {newInstance.InstanceId}");
                }
                else
                {
                    System.Console.WriteLine($"Tests failed for instance {newInstance.InstanceId}");

                    // Handle test failure, e.g., terminate the instance or take corrective action
                    // You may want to add your specific logic here
                }
            }
        }

        // Step 7: Drain or keep old instances based on test results
        // Add code here to handle old instances based on test results
        private void KeepOldInstances(List<string> oldInstanceIds)
        {
            foreach (var oldInstanceId in oldInstanceIds) // Replace oldInstanceIds with the actual list of old instance IDs
            {
                System.Console.WriteLine($"Checking old instance {oldInstanceId}...");

                // Replace this with your actual decision-making logic
                bool shouldDrainInstance = ShouldDrainOldInstance(oldInstanceId);

                if (shouldDrainInstance)
                {
                    System.Console.WriteLine($"Draining old instance {oldInstanceId}...");
                    // Add logic to gracefully drain the old instance, e.g., redirect traffic, remove from load balancer, etc.
                }
                else
                {
                    System.Console.WriteLine($"Keeping old instance {oldInstanceId}...");
                    // No action required for this old instance
                }
            }
        }



        public List<string> GetOldInstanceIds()
        {
            return new List<string>
            {
                "old-instance-id-1",
                "old-instance-id-2",
                // Add more old instance IDs as needed
            };
        }

        public async Task<List<string>> GetOldInstanceIdsByTag(AmazonEC2Client ec2Client)
        {
            List<string> oldInstanceIds = new List<string>();

            var describeInstancesRequest = new DescribeInstancesRequest
            {
                Filters = new List<Amazon.EC2.Model.Filter>
                {
                    new Amazon.EC2.Model.Filter
                    {
                        Name = "tag:Lifecycle",
                        Values = new List<string> { "old" } // Replace with your tag value
                    }
                }
            };

            var describeInstancesResponse = await ec2Client.DescribeInstancesAsync(describeInstancesRequest);

            foreach (var reservation in describeInstancesResponse.Reservations)
            {
                foreach (var instance in reservation.Instances)
                {
                    oldInstanceIds.Add(instance.InstanceId);
                }
            }

            return oldInstanceIds;
        }

        bool ShouldDrainOldInstance(string oldInstanceId)
        {
            // Implement your logic to decide whether to drain or keep the old instance
            // Return true if you want to drain it, false if you want to keep it

            // For simplicity, assume we want to drain all old instances
            return true;
        }

        void TerminateInstance(IAmazonEC2 ec2Client, string instanceId)
        {
            var terminateInstancesRequest = new TerminateInstancesRequest
            {
                InstanceIds = new List<string> { instanceId }
            };

            var terminateInstancesResponse = ec2Client.TerminateInstancesAsync(terminateInstancesRequest);
            // You may want to handle the response or errors here
        }

        bool RunCustomTests(string instanceId)
        {
            // Implement your custom testing logic here
            // You can use the EC2 instance ID to SSH into the instance, execute tests, and retrieve results
            // Return true if tests pass, false otherwise
            // You may want to capture test results, log them, or take specific actions based on the test outcome

            // For simplicity, assume tests pass
            return true;
        }

        
    }
}

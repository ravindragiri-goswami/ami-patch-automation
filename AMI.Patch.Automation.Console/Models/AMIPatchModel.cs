namespace AMI.Patch.Automation.Console.Models
{
    public class AMIPatchModel
    {
        public string InstanceId { get; set; } // instance ID
        public string NewInstanceUserData { get; set; } // user data script
        public string NewInstanceType { get; set; } // desired instance type
        public string NewAMIName { get; set; } // AMI name
        public string NewAMIDescription { get; set; } // AMI description
    }
}

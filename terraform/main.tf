# Configure the AWS provider
provider "aws" {
  region = "us-east-1" # Set your desired region
}

# Create a VPC
resource "aws_vpc" "example" {
  cidr_block = "10.0.0.0/16" # Replace with your desired CIDR block
  enable_dns_support = true
  enable_dns_hostnames = true
  tags = {
    Name = "example-vpc"
  }
}

# Create an Internet Gateway
resource "aws_internet_gateway" "example" {
  vpc_id = aws_vpc.example.id
  tags = {
    Name = "example-igw"
  }
}

# Create a public subnet
resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.example.id
  cidr_block              = "10.0.1.0/24" # Replace with your desired CIDR block for the public subnet
  availability_zone       = "us-east-1a" # Replace with your desired availability zone
  map_public_ip_on_launch = true
  tags = {
    Name = "example-public-subnet"
  }
}

# Create a private subnet
resource "aws_subnet" "private" {
  vpc_id                  = aws_vpc.example.id
  cidr_block              = "10.0.2.0/24" # Replace with your desired CIDR block for the private subnet
  availability_zone       = "us-east-1b" # Replace with your desired availability zone
  map_public_ip_on_launch = false
  tags = {
    Name = "example-private-subnet"
  }
}


# Create an IAM instance profile for EC2 instances
resource "aws_iam_instance_profile" "example" {
  name = "example-instance-profile"
  role = aws_iam_role.example.name
}

# Create an IAM role for EC2 instances (customize as needed)
resource "aws_iam_role" "example" {
  name = "example-role"

  # Assume role policy document
  assume_role_policy = jsonencode({
    Version = "2012-10-17",
    Statement = [
      {
        Action = "sts:AssumeRole",
        Effect = "Allow",
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_policy" "cloudwatch_logs_policy" {
  name = "example-cloudwatch-logs-policy"

  # Define the permissions for CloudWatch Logs here
  description = "Example policy for CloudWatch Logs"

  policy = jsonencode({
    Version = "2012-10-17",
    Statement = [
      {
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:DescribeLogStreams",
        ],
        Effect   = "Allow",
        Resource = "*"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "cloudwatch_logs_attachment" {
  policy_arn = aws_iam_policy.cloudwatch_logs_policy.arn
  role       = aws_iam_role.example.name
}

# Create a security group for your EC2 instances (customize as needed)
resource "aws_security_group" "example" {
  name        = "example-sg"
  description = "Security group for example EC2 instances"

  # Add rules to allow necessary traffic (e.g., HTTP)
  # Example rule for HTTP traffic:
  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# Create a launch configuration for the Auto Scaling Group
resource "aws_launch_configuration" "example" {
  name_prefix          = "example-lc-"
  image_id             = "ami-0c55b159cbfafe1f0" # Replace with your AMI ID
  instance_type        = "t2.micro"              # Adjust instance type as needed
  iam_instance_profile = aws_iam_instance_profile.example.name
  security_groups      = [aws_security_group.example.name]

  # User data script to deploy your .NET Core 6 Web API
  user_data = <<-EOF
    #!/bin/bash

    # Update the system and install necessary packages
    sudo apt-get update -y
    sudo apt-get install -y apt-transport-https ca-certificates curl software-properties-common

    # Add the .NET Core 6 repository and install .NET SDK
    wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    sudo apt-get update -y
    sudo apt-get install -y dotnet-sdk-6.0 # Adjust the version as needed

    # Optionally, you can set environment variables here if needed
    # export YOUR_ENV_VAR=value

    # Clone your .NET Web API repository (replace with your actual repository URL)
    git clone https://github.com/your-repo/your-dotnet-web-api.git /app

    # Navigate to the API directory
    cd /app

    # Build and publish the .NET Core 6 Web API
    dotnet publish -c Release -o /app/published

    # Start the .NET Core 6 Web API as a background process
    nohup dotnet /app/published/YourApi.dll &

    # Optionally, configure any additional settings or environment variables here

    # End of user data script
  EOF
}


# Create an Auto Scaling Group
resource "aws_autoscaling_group" "example" {
  name                      = "example-asg"
  launch_configuration      = aws_launch_configuration.example.name
  min_size                  = 1
  max_size                  = 3 # Adjust as needed
  desired_capacity          = 1
  vpc_zone_identifier       = [aws_subnet.private.id] # Replace with your subnet ID
  health_check_type         = "EC2"
  health_check_grace_period = 300 # Adjust as needed

  # Add scaling policies as needed based on your scaling requirements
  # Example scaling policy:
  # scaling_policies = [{
  #   name                      = "example-scaling-policy"
  #   adjustment_type           = "ChangeInCapacity"
  #   scaling_adjustment        = 1
  #   cooldown                  = 300
  #   policy_type               = "SimpleScaling"
  # }]
}

# Create a Load Balancer
resource "aws_lb" "example" {
  name               = "example-lb"
  internal           = false
  load_balancer_type = "application"
  subnets            = [aws_subnet.public.id] # Replace with your subnet IDs
}

# Create a target group for the Auto Scaling Group
resource "aws_lb_target_group" "example" {
  name     = "example-target-group"
  port     = 80
  protocol = "HTTP"
  vpc_id   = aws_vpc.example.id # Replace with your VPC ID

  health_check {
    path                = "/"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 2
  }

  target_type = "instance"
}

# Attach the Auto Scaling Group to the target group
resource "aws_autoscaling_attachment" "example" {
  autoscaling_group_name = aws_autoscaling_group.example.name
  lb_target_group_arn    = aws_lb_target_group.example.arn
}

# ============================================================
# Tarea 83: Infrastructure as Code — Terraform
# Provider: AWS (ECS Fargate + RDS Postgres + ElastiCache Redis)
# ============================================================

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
  backend "s3" {
    bucket = "encorely-terraform-state"
    key    = "prod/terraform.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

variable "aws_region" {
  default = "us-east-1"
}

variable "db_password" {
  sensitive = true
}

# --- VPC ---
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "5.1.2"

  name = "encorely-vpc"
  cidr = "10.0.0.0/16"

  azs             = ["us-east-1a", "us-east-1b"]
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

  enable_nat_gateway = true
  single_nat_gateway = true
}

# --- RDS PostgreSQL ---
resource "aws_db_instance" "encorely_postgres" {
  identifier        = "encorely-postgres"
  engine            = "postgres"
  engine_version    = "16.1"
  instance_class    = "db.t3.medium"
  allocated_storage = 20
  db_name           = "encorely_db"
  username          = "admin"
  password          = var.db_password

  vpc_security_group_ids = [aws_security_group.rds_sg.id]
  db_subnet_group_name   = aws_db_subnet_group.encorely.name

  backup_retention_period = 7
  deletion_protection     = true
  skip_final_snapshot     = false
  final_snapshot_identifier = "encorely-final-snapshot"
}

resource "aws_db_subnet_group" "encorely" {
  name       = "encorely-db-subnet-group"
  subnet_ids = module.vpc.private_subnets
}

# --- ElastiCache Redis ---
resource "aws_elasticache_cluster" "encorely_redis" {
  cluster_id           = "encorely-redis"
  engine               = "redis"
  node_type            = "cache.t3.micro"
  num_cache_nodes      = 1
  parameter_group_name = "default.redis7"
  port                 = 6379
  subnet_group_name    = aws_elasticache_subnet_group.encorely.name
}

resource "aws_elasticache_subnet_group" "encorely" {
  name       = "encorely-redis-subnet"
  subnet_ids = module.vpc.private_subnets
}

# --- Security Groups ---
resource "aws_security_group" "rds_sg" {
  name   = "encorely-rds-sg"
  vpc_id = module.vpc.vpc_id

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["10.0.0.0/16"]
  }
}

# --- Outputs ---
output "postgres_endpoint" {
  value = aws_db_instance.encorely_postgres.endpoint
}

output "redis_endpoint" {
  value = aws_elasticache_cluster.encorely_redis.cache_nodes[0].address
}

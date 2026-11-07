variable "name_prefix" { 
    type = string 
}

variable "atlas_connection_string" {
    type = string 
}

variable "jwt_secret" {
    type = string
}

variable "rabbitmq_password" {
    type = string
}

variable "brevo_api_key" {
    type = string
}

variable "brevo_sender_email" {
    type = string
}

variable "grafana_otlp_headers" {
    type = string
}

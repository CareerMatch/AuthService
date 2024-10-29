namespace AuthServices.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

public class AuthModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } = "User";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Token storage fields
        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiry { get; set; }
    }


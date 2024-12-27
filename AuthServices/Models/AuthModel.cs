namespace AuthServices.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.ComponentModel.DataAnnotations;

public class AuthModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; } = Guid.NewGuid(); // Shared User ID
    
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [MinLength(8)]
    public string PasswordHash { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "User";

    // Token storage fields
    public string RefreshToken { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
}



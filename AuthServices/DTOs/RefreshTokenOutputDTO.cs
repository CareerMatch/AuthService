namespace AuthServices.DTOs;

public class RefreshTokenOutputDTO
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}
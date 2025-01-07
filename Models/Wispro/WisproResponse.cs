namespace Exatech_Indotel_API.Models.Wispro
{
    public class WisproResponse<T> where T : class
    {
        public int Status { get; set; }
        public object? Meta { get; set; }
        public T? Data { get; set; }
    }
}

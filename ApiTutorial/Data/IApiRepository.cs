using SampleApi.Dtos;
using SampleApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleApi.Data
{
    public interface IApiRepository
    {
        Task<IEnumerable<Product>> GetProducts();
        Task<Product> GetProductById(int id);
        Task<Product> AddProduct(ProductDto product);
        Task<Product> UpdateProduct(ProductDto product);
        Task DeleteProduct(int id);
    }
}

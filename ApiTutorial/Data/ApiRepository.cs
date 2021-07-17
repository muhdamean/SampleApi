using SampleApi.Dtos;
using SampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleApi.Data
{
    public class ApiRepository : IApiRepository
    {
        private readonly ILogger<ApiRepository> logger;
        private readonly AppDbContext appDbContext;

        public ApiRepository(ILogger<ApiRepository> logger, AppDbContext appDbContext)
        {
            this.logger = logger;
            this.appDbContext = appDbContext;
        }

        public async Task<Product> AddProduct(ProductDto product)
        {
            var newProduct = new Product
            {
                Name = product.Name,
                Category = product.Category,
                Description = product.Description,
                Price = product.Price,
                DateCreated = product.DateCreated
            };
            await appDbContext.Products.AddAsync(newProduct);
            var result= await appDbContext.SaveChangesAsync();
            return newProduct;
        }

        public async Task DeleteProduct(int id)
        {
            var delete = await appDbContext.Products.FirstOrDefaultAsync(x => x.Id == id);
            if (delete!=null)
            {
                appDbContext.Products.Remove(delete);
                await appDbContext.SaveChangesAsync();
            }
        }

        public async Task<Product> GetProductById(int id)
        {
            return await appDbContext.Products.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<Product>> GetProducts()
        {
            return  await appDbContext.Products.ToListAsync();
        }

        public async Task<Product> UpdateProduct(ProductDto product)
        {
            var check =await appDbContext.Products.FirstOrDefaultAsync(x => x.Id == product.Id);
            if (check==null)
            {
                return null;
            }
            check.Name = product.Name;
            check.Price = product.Price;
            check.Description = product.Description;
            check.Category = product.Category;
            appDbContext.Products.Update(check);
            var result = await appDbContext.SaveChangesAsync();
            return check;
        }
    }
}

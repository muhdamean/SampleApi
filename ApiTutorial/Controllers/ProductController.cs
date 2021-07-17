using SampleApi.Data;
using SampleApi.Dtos;
using SampleApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleApi.Controllers
{
    [Route("api/[controller]")]
    //[Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
   // [ApiVersion("1.0", Deprecated =true)]
    [ApiVersion("1.0")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ProductController : ControllerBase
    {
        private readonly ILogger<ProductController> logger;
        private readonly AppDbContext appDbContext;
        private readonly IApiRepository apiRepository;

        public ProductController(ILogger<ProductController> logger, AppDbContext dbContext, IApiRepository apiRepository)
        {
            this.logger = logger;
            this.appDbContext = dbContext;
            this.apiRepository = apiRepository;
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            var products= await apiRepository.GetProductById(id);
            if (products==null)
            {
                return NotFound(StatusCode(StatusCodes.Status404NotFound,$"Product with Id {id} not found"));
            }
            return Ok(products);
        }
        [HttpGet]
        [Route("products")]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await apiRepository.GetProducts();
                if (products==null)
                {
                    return NotFound();
                }
                return Ok(products);
            }
            catch (Exception)
            {
                return NotFound(StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving data from database"));
            }
        }
        [HttpPost]
        [Route("CreateProduct")]
        public async Task<ActionResult<Product>> CreateProduct(ProductDto product)
        {
            try
            {
                if (product == null)
                {
                    return BadRequest();
                }
                var createdProduct =await apiRepository.AddProduct(product);

                return CreatedAtAction(nameof(GetProductById), new { id = createdProduct.Id }, createdProduct);
            }
            catch (Exception)
            {
                return NotFound(StatusCode(StatusCodes.Status500InternalServerError, "Error creating new product"));
            }
        }
        [HttpPut("{id:int}")]
        //[Route("UpdateProduct/{id}")]
        public async Task<ActionResult<Product>> UpdateProduct(int id, ProductDto product)
        {
            try
            {
                if (id != product.Id)
                {
                    return BadRequest("Employee Id mismatch");
                }
                var productToUpdate = await GetProductById(id);
                if (productToUpdate == null)
                {
                    return NotFound($"Employee with the Id= {id} not found");
                }
                return await apiRepository.UpdateProduct(product);
            }
            catch (Exception)
            {
                return NotFound(StatusCode(StatusCodes.Status500InternalServerError, "Error updating the product"));
            }
        }
        [HttpDelete("{id}")]
        //[Route("DeleteProductById")]
        public async Task<ActionResult> DeleteProduct(int id)
        {
            try
            {
                var check = await apiRepository.GetProductById(id);
                if (check==null)
                {
                    return NotFound($"product with Id={id} not found");
                }
                await apiRepository.DeleteProduct(check.Id);
                return Ok($"Product with Id= {id} deleted");
            }
            catch (Exception)
            {
                return NotFound(StatusCode(StatusCodes.Status500InternalServerError, "Error deleting the product"));
            }
        }
    }
}

using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Application.Services;

public class ProductsManagementService : IProductsManagementService
{
    private readonly IRepository _repository;

    public ProductsManagementService(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductModel.ProductResponseUpdate>? GetProductById(Guid id)
    {
        var product = await _repository.GetById<Product>(id);
        if (product == null) throw new EntityNotFoundException("Producto no encontrado");

        return new ProductModel.ProductResponseUpdate(
            product.Id,
            product.Sku,
            product.Name,
            product.CurrentUnitPrice,
            product.InternalCode,
            product.Description,
            product.StockQuantity,
            product.IsActive
        );
    }

    public async Task<ProductModel.ResponsePagination?> GetProducts(ProductModel.FilterProduct request)
    {
        bool? isActive = request?.Status == "enabled"
            ? (bool?)true
            : request?.Status == "disabled"
                ? (bool?)false
                : null;

        var search = request?.Search ?? string.Empty;
        var searchLower = search.ToLower();

        var matched = await _repository.GetFiltered<Product>(p =>
            (isActive == null || p.IsActive == isActive) &&
            (string.IsNullOrEmpty(searchLower) ||
                (p.Name != null && p.Name.ToLower().Contains(searchLower)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(searchLower)) ||
                (p.InternalCode != null && p.InternalCode.ToLower().Contains(searchLower)))
        );

        if (matched is null || !matched.Any())
            throw new EntityNotFoundException("No products were found");

        var productsQuery = matched
            .Select(p => new ProductModel.ProductResponse(
                p.Id,
                p.Sku,
                p.Name,
                p.CurrentUnitPrice,
                p.InternalCode,
                p.Description,
                p.StockQuantity,
                p.IsActive
            ))
            .OrderBy(p => p.Sku);

        var pageNumber = request?.PageNumber <= 0 ? 1 : (request?.PageNumber ?? 1);
        var pageSize = request?.PageSize <= 0 ? matched.Count() : (request?.PageSize ?? matched.Count());

        var paged = productsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ProductModel.ResponsePagination(paged, matched.Count());
    }

    public async Task<List<ProductModel.ProductResponseUpdate>?> GetProducts()
    {
        var products = await _repository.GetAll<Product>();
        if (products == null || !products.Any() || products.Where(p => p.IsActive) == null)
        {
            throw new EntityNotFoundException("No se encontraron productos activos.");
        }

        return products.Where(p => p.IsActive == true).Select(p => new ProductModel.ProductResponseUpdate(
            p.Id,
            p.Sku,
            p.Name,
            p.CurrentUnitPrice,
            p.InternalCode,
            p.Description,
            p.StockQuantity,
            p.IsActive
        )).ToList();
    }

    public async Task<ProductModel.ProductResponse> AddProduct(ProductModel.ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku) ||
            string.IsNullOrWhiteSpace(request.InternalCode) ||
            string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            request.StockQuantity <= 0)
        {
            throw new ArgumentException("Valores de stock, para el producto, no validos");
        }
        if (request.CurrentUnitPrice <= 0) throw new PriceNullException("El precio del producto no puede ser cero o menor.");
        var exist = await _repository.First<Product>(p => p.Sku == request.Sku);
        if (exist != null) throw new DuplicatedEntityException($"Ya existe un producto con el Sku {request.Sku}");

        var product = new Product(request.Sku, request.InternalCode, request.Name, request.Description, request.CurrentUnitPrice, request.StockQuantity);

        await _repository.Add(product);
        return new ProductModel.ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.CurrentUnitPrice,
            product.InternalCode,
            product.Description,
            product.StockQuantity,
            product.IsActive
        );
    }

    public async Task<bool> DisableProductAsync(Guid id)
    {
        var product = await _repository.GetById<Product>(id);
        if (product is null || !product.IsActive)
            throw new EntityNotFoundException("Producto no encontrado o ya deshabilitado.");

        product.IsActive = false;
        await _repository.Update(product);
        return true;
    }

    public async Task<ProductModel.ProductResponseUpdate> UpdateAsync(ProductModel.ProductRequest request, Guid id)
    {
        var product = await _repository.GetById<Product>(id);
        if (product == null)
            throw new EntityNotFoundException($"Producto con ID {id} no encontrado.");
        if (request == null ||
                string.IsNullOrWhiteSpace(request.Sku) ||
                string.IsNullOrWhiteSpace(request.InternalCode) ||
                string.IsNullOrWhiteSpace(request.Name) ||
                request.CurrentUnitPrice <= 0)
            throw new ArgumentException("Valores para el producto no validos");

        product.Sku = request.Sku;
        product.InternalCode = request.InternalCode;
        product.Name = request.Name;
        product.Description = request.Description;
        product.CurrentUnitPrice = request.CurrentUnitPrice;
        product.StockQuantity = request.StockQuantity;

        await _repository.Update(product);

        return new ProductModel.ProductResponseUpdate(
            product.Id,
            product.Sku,
            product.Name,
            product.CurrentUnitPrice,
            product.InternalCode,
            product.Description,
            product.StockQuantity,
            product.IsActive
        );
    }
}
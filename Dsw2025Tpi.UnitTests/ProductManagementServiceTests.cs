using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests
{
    public class ProductManagementServiceTests
    {
        private readonly Mock<IRepository> _repositoryMock;
        private readonly ProductsManagementService _service;

        public ProductManagementServiceTests()
        {
            _repositoryMock = new Mock<IRepository>();
            _service = new ProductsManagementService(_repositoryMock.Object);
        }

        #region Creación de productos (Admin)

        [Fact]
        public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
        {
            // Arrange
            var request = new ProductModel.ProductRequest(
                Sku: "SKU-001",
                InternalCode: "INT-001",
                Name: "Mouse Gamer",
                Description: "Mouse óptico RGB",
                CurrentUnitPrice: 25000m,
                StockQuantity: 10
            );

            _repositoryMock
                .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                .ReturnsAsync((Product?)null);

            _repositoryMock
                .Setup(r => r.Add(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            // Act
            var result = await _service.AddProduct(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Sku, result.Sku);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.CurrentUnitPrice, result.Price);
            Assert.Equal(request.StockQuantity, result.Stock);
            Assert.True(result.isActive);

            _repositoryMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        }

        [Fact]
        public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
        {
            // Arrange
            var request = new ProductModel.ProductRequest(
                Sku: "SKU-DUP",
                InternalCode: "INT-002",
                Name: "Teclado Mecánico",
                Description: "Switch Red",
                CurrentUnitPrice: 45000m,
                StockQuantity: 5
            );

            var existingProduct = new Product("SKU-DUP", "INT-001", "Otro Producto", "Desc", 1000m, 2);

            _repositoryMock
                .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                .ReturnsAsync(existingProduct);

            // Act & Assert
            await Assert.ThrowsAsync<DuplicatedEntityException>(() => _service.AddProduct(request));

            _repositoryMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        }

        [Theory]
        [InlineData(0, 1000)]    // Stock == 0
        [InlineData(-5, 1000)]   // Stock < 0
        [InlineData(10, -100)]   // Precio < 0
        [InlineData(10, 0)]      // Precio == 0
        public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(int stock, decimal price)
        {
            // Arrange
            var request = new ProductModel.ProductRequest(
                Sku: "SKU-INV",
                InternalCode: "INT-INV",
                Name: "Producto Inválido",
                Description: "Descripción",
                CurrentUnitPrice: price,
                StockQuantity: stock
            );

            // Act & Assert
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => _service.AddProduct(request));
            Assert.True(exception is ArgumentException || exception is PriceNullException);

            _repositoryMock.Verify(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()), Times.Never);
            _repositoryMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        }

        #endregion

        #region Listado de productos (Admin)

        [Fact]
        public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product("SKU-1", "INT-1", "Producto Activo 1", "Desc", 100m, 5) { IsActive = true },
                new Product("SKU-2", "INT-2", "Producto Inactivo", "Desc", 200m, 5) { IsActive = false },
                new Product("SKU-3", "INT-3", "Producto Activo 2", "Desc", 300m, 5) { IsActive = true }
            };

            _repositoryMock
                .Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                    products.Where(predicate.Compile()).ToList());

            var filter = new ProductModel.FilterProduct(Status: "enabled", Search: null, PageNumber: 1, PageSize: 10);

            // Act
            var result = await _service.GetProducts(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result!.ProductItems.Count);
            Assert.All(result.ProductItems, p => Assert.True(p.isActive));
        }

        [Fact]
        public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
        {
            // Arrange
            var products = Enumerable.Range(1, 15)
                .Select(i => new Product($"SKU-{i:D2}", $"INT-{i:D2}", $"Producto {i}", "Desc", 100m * i, 10))
                .ToList();

            _repositoryMock
                .Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                    products.Where(predicate.Compile()).ToList());

            var filter = new ProductModel.FilterProduct(Status: null, Search: null, PageNumber: 2, PageSize: 5);

            // Act
            var result = await _service.GetProducts(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result!.ProductItems.Count);
            Assert.Equal(15, result.Total);
            Assert.Equal("SKU-06", result.ProductItems.First().Sku);
            Assert.Equal("SKU-10", result.ProductItems.Last().Sku);
        }

        [Fact]
        public async Task GetProducts_WithSearchTerm_FiltersByNameSkuOrInternalCode()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product("SKU-100", "INT-100", "Mouse Gamer", "Desc", 15000m, 10),
                new Product("SKU-200", "INT-200", "Teclado Mecánico", "Desc", 35000m, 8),
                new Product("SKU-300", "INT-300", "Monitor", "Desc", 120000m, 4)
            };

            _repositoryMock
                .Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
                .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                    products.Where(predicate.Compile()).ToList());

            var filter = new ProductModel.FilterProduct(Status: null, Search: "mouse", PageNumber: 1, PageSize: 10);

            // Act
            var result = await _service.GetProducts(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result!.ProductItems);
            Assert.Equal("Mouse Gamer", result.ProductItems.First().Name);
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
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
    public class OrderManagementServiceTests
    {
        private readonly Mock<IRepository> _repositoryMock;
        private readonly OrderManagement _service;

        public OrderManagementServiceTests()
        {
            _repositoryMock = new Mock<IRepository>();
            _service = new OrderManagement(_repositoryMock.Object);
        }

        [Fact]
        public async Task CreateOrder_WithValidItemsAndStock_CalculatesTotalAndDecrementsProductStock()
        {
         
            
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var customer = new Customer("cliente@test.com", "Juan Pérez", "3815555555")
            {
                Id = customerId
            };

            var product = new Product("SKU-01", "INT-01", "Auriculares", "Inalámbricos", 15000m, 10)
            {
                Id = productId,
                IsActive = true
            };

            var request = new OrderModel.OrderRequest(
                ShippingAddress: "Av. Independencia 1800",
                BillingAddress: "Av. Independencia 1800",
                CustomerId: customerId,
                OrderItems: new List<OrderModel.OrderItemModel>
                {
                    new OrderModel.OrderItemModel(ProductId: productId, Quantity: 3)
                }
            );

            _repositoryMock
                .Setup(r => r.GetById<Customer>(customerId, It.IsAny<string[]>()))
                .ReturnsAsync(customer);

            _repositoryMock
                .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
                .ReturnsAsync(product);

            _repositoryMock
                .Setup(r => r.Update(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            _repositoryMock
                .Setup(r => r.Add(It.IsAny<Order>()))
                .ReturnsAsync((Order o) => o);

            // Act
            var result = await _service.AddOrder(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(45000m, result.TotalAmount); // 3 * 15000
            Assert.Equal(7, product.StockQuantity);    // 10 - 3

            _repositoryMock.Verify(r => r.Update(It.Is<Product>(p => p.Id == productId && p.StockQuantity == 7)), Times.Once);
            _repositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Once);
        }

        [Fact]
        public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var customer = new Customer("cliente@test.com", "Juan Pérez", "3815555555")
            {
                Id = customerId
            };

            var product = new Product("SKU-02", "INT-02", "Webcam", "Full HD", 20000m, 2)
            {
                Id = productId,
                IsActive = true
            };

            var request = new OrderModel.OrderRequest(
                ShippingAddress: "Calle Falsa 123",
                BillingAddress: "Calle Falsa 123",
                CustomerId: customerId,
                OrderItems: new List<OrderModel.OrderItemModel>
                {
                    new OrderModel.OrderItemModel(ProductId: productId, Quantity: 5)
                }
            );

            _repositoryMock
                .Setup(r => r.GetById<Customer>(customerId, It.IsAny<string[]>()))
                .ReturnsAsync(customer);

            _repositoryMock
                .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
                .ReturnsAsync(product);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.AddOrder(request));
            Assert.Contains("suficiente stock", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, product.StockQuantity); // El stock no debe alterarse

            _repositoryMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
            _repositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task CreateOrder_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
        {
            // Arrange
            var nonExistentCustomerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var request = new OrderModel.OrderRequest(
                ShippingAddress: "Calle Falsa 123",
                BillingAddress: "Calle Falsa 123",
                CustomerId: nonExistentCustomerId,
                OrderItems: new List<OrderModel.OrderItemModel>
                {
                    new OrderModel.OrderItemModel(ProductId: productId, Quantity: 1)
                }
            );

            _repositoryMock
                .Setup(r => r.GetById<Customer>(nonExistentCustomerId, It.IsAny<string[]>()))
                .ReturnsAsync((Customer?)null);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.AddOrder(request));

            _repositoryMock.Verify(r => r.GetById<Product>(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
            _repositoryMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
            _repositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        }
    }
}  

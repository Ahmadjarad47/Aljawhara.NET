# Endpoint inventory

The working tree contains 213 attribute-routed controller actions in 24 controllers. The tables use the route spelling and parameter constraints declared in source.

JWT denotes the default bearer authentication scheme. Admin JWT requires the `Admin` role, either directly or through `AdminOnly`. Public denotes MVC transport access. Actions with application-level credentials name them in the last column. The `UserOrAdmin` policy is defined, and the route scan found no action applying it. The API controller convention applies the `api` limiter. [Authentication and authorization](../Ecom.API/Program.cs#L299) · [Controller mapping](../Ecom.API/Program.cs#L523)

## AdminAuth

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/Auth/isAdmin` | Admin JWT | [Get](../Ecom.API/Controllers/AdminAuthController.cs#L32) |  |
| GET | `/api/admin/Auth/user/{userId}` | Admin JWT | [GetUser](../Ecom.API/Controllers/AdminAuthController.cs#L38) |  |
| GET | `/api/admin/Auth/users` | Admin JWT | [GetAllUsers](../Ecom.API/Controllers/AdminAuthController.cs#L57) |  |
| POST | `/api/admin/Auth/user/{userId}/addresses` | Admin JWT | [CreateAddressForUser](../Ecom.API/Controllers/AdminAuthController.cs#L73) |  |
| GET | `/api/admin/Auth/user/{userId}/addresses` | Admin JWT | [GetUserAddresses](../Ecom.API/Controllers/AdminAuthController.cs#L92) |  |
| GET | `/api/admin/Auth/user/{userId}/addresses/{addressId}` | Admin JWT | [GetAddress](../Ecom.API/Controllers/AdminAuthController.cs#L106) |  |
| PUT | `/api/admin/Auth/user/{userId}/addresses/{addressId}` | Admin JWT | [UpdateAddress](../Ecom.API/Controllers/AdminAuthController.cs#L124) |  |
| DELETE | `/api/admin/Auth/user/{userId}/addresses/{addressId}` | Admin JWT | [DeleteAddress](../Ecom.API/Controllers/AdminAuthController.cs#L152) |  |
| POST | `/api/admin/Auth/user/{userId}/addresses/{addressId}/set-default` | Admin JWT | [SetDefaultAddress](../Ecom.API/Controllers/AdminAuthController.cs#L170) |  |

## AdminCarousel

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/carousels` | Admin JWT | [GetCarousels](../Ecom.API/Controllers/AdminCarouselController.cs#L21) |  |
| GET | `/api/admin/carousels/{id}` | Admin JWT | [GetCarousel](../Ecom.API/Controllers/AdminCarouselController.cs#L46) |  |
| POST | `/api/admin/carousels` | Admin JWT | [CreateCarousel](../Ecom.API/Controllers/AdminCarouselController.cs#L57) |  |
| PUT | `/api/admin/carousels/{id}` | Admin JWT | [UpdateCarousel](../Ecom.API/Controllers/AdminCarouselController.cs#L77) |  |
| DELETE | `/api/admin/carousels/{id}` | Admin JWT | [DeleteCarousel](../Ecom.API/Controllers/AdminCarouselController.cs#L103) |  |
| PUT | `/api/admin/carousels/{id}/toggle-active` | Admin JWT | [ToggleCarouselActive](../Ecom.API/Controllers/AdminCarouselController.cs#L121) |  |

## AdminCategories

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/categories` | Admin JWT | [GetCategories](../Ecom.API/Controllers/AdminCategoriesController.cs#L21) |  |
| GET | `/api/admin/categories/{id}` | Admin JWT | [GetCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L35) |  |
| POST | `/api/admin/categories` | Admin JWT | [CreateCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L46) |  |
| POST | `/api/admin/categories/with-image` | Admin JWT | [CreateCategoryWithImage](../Ecom.API/Controllers/AdminCategoriesController.cs#L65) |  |
| DELETE | `/api/admin/categories/bulk` | Admin JWT | [bulk](../Ecom.API/Controllers/AdminCategoriesController.cs#L83) |  |
| PUT | `/api/admin/categories/{id}` | Admin JWT | [UpdateCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L107) |  |
| PUT | `/api/admin/categories/{id}/with-image` | Admin JWT | [UpdateCategoryWithImage](../Ecom.API/Controllers/AdminCategoriesController.cs#L135) |  |
| DELETE | `/api/admin/categories/{id}` | Admin JWT | [DeleteCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L159) |  |
| GET | `/api/admin/categories/subcategories` | Admin JWT | [GetSubCategories](../Ecom.API/Controllers/AdminCategoriesController.cs#L181) |  |
| GET | `/api/admin/categories/{id}/subcategories` | Admin JWT | [GetSubCategories](../Ecom.API/Controllers/AdminCategoriesController.cs#L196) |  |
| POST | `/api/admin/categories/subcategories` | Admin JWT | [CreateSubCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L218) |  |
| GET | `/api/admin/categories/subcategories/{id}` | Admin JWT | [GetSubCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L237) |  |
| PUT | `/api/admin/categories/subcategories/{id}` | Admin JWT | [UpdateSubCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L248) |  |
| DELETE | `/api/admin/categories/subcategories/{id}` | Admin JWT | [DeleteSubCategory](../Ecom.API/Controllers/AdminCategoriesController.cs#L276) |  |
| PUT | `/api/admin/categories/{id}/toggle-active` | Admin JWT | [ToggleCategoryActive](../Ecom.API/Controllers/AdminCategoriesController.cs#L298) |  |
| PUT | `/api/admin/categories/subcategories/{id}/toggle-active` | Admin JWT | [ToggleSubCategoryActive](../Ecom.API/Controllers/AdminCategoriesController.cs#L334) |  |
| GET | `/api/admin/categories/all` | Admin JWT | [GetAllCategories](../Ecom.API/Controllers/AdminCategoriesController.cs#L370) |  |
| GET | `/api/admin/categories/subcategories/all` | Admin JWT | [GetAllSubCategories](../Ecom.API/Controllers/AdminCategoriesController.cs#L385) |  |

## AdminCoupons

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/coupons` | Admin JWT | [GetCoupons](../Ecom.API/Controllers/AdminCouponsController.cs#L21) |  |
| GET | `/api/admin/coupons/active` | Admin JWT | [GetActiveCoupons](../Ecom.API/Controllers/AdminCouponsController.cs#L39) |  |
| GET | `/api/admin/coupons/expired` | Admin JWT | [GetExpiredCoupons](../Ecom.API/Controllers/AdminCouponsController.cs#L57) |  |
| GET | `/api/admin/coupons/{id}` | Admin JWT | [GetCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L75) |  |
| GET | `/api/admin/coupons/code/{code}` | Admin JWT | [GetCouponByCode](../Ecom.API/Controllers/AdminCouponsController.cs#L86) |  |
| POST | `/api/admin/coupons` | Admin JWT | [CreateCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L97) |  |
| PUT | `/api/admin/coupons/{id}` | Admin JWT | [UpdateCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L120) |  |
| DELETE | `/api/admin/coupons/{id}` | Admin JWT | [DeleteCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L148) |  |
| PUT | `/api/admin/coupons/{id}/activate` | Admin JWT | [ActivateCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L166) |  |
| PUT | `/api/admin/coupons/{id}/deactivate` | Admin JWT | [DeactivateCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L184) |  |
| POST | `/api/admin/coupons/cleanup-expired` | Admin JWT | [CleanupExpiredCoupons](../Ecom.API/Controllers/AdminCouponsController.cs#L202) |  |
| POST | `/api/admin/coupons/validate` | Admin JWT | [ValidateCoupon](../Ecom.API/Controllers/AdminCouponsController.cs#L220) |  |

## AdminDashboard

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/dashboard/summary` | Admin JWT | [GetSummary](../Ecom.API/Controllers/AdminDashboardController.cs#L19) |  |
| GET | `/api/admin/dashboard/last-orders` | Admin JWT | [GetLastOrders](../Ecom.API/Controllers/AdminDashboardController.cs#L26) |  |
| GET | `/api/admin/dashboard/last-users` | Admin JWT | [GetLastUsers](../Ecom.API/Controllers/AdminDashboardController.cs#L33) |  |
| GET | `/api/admin/dashboard/visitors/chart` | Admin JWT | [GetVisitorsChart](../Ecom.API/Controllers/AdminDashboardController.cs#L40) |  |
| GET | `/api/admin/dashboard/users/chart` | Admin JWT | [GetUsersChart](../Ecom.API/Controllers/AdminDashboardController.cs#L48) |  |
| GET | `/api/admin/dashboard/orders/chart` | Admin JWT | [GetOrdersChart](../Ecom.API/Controllers/AdminDashboardController.cs#L56) |  |
| GET | `/api/admin/dashboard/transactions/chart` | Admin JWT | [GetTransactionsChart](../Ecom.API/Controllers/AdminDashboardController.cs#L64) |  |

## AdminHealth

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/health/summary` | Admin JWT | [GetSummary](../Ecom.API/Controllers/AdminHealthController.cs#L19) |  |
| GET | `/api/admin/health/chart` | Admin JWT | [GetChart](../Ecom.API/Controllers/AdminHealthController.cs#L26) |  |

## AdminMonitoring

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/monitoring/summary` | Admin JWT | [Summary](../Ecom.API/Controllers/AdminMonitoringController.cs#L14) |  |
| GET | `/api/admin/monitoring/logs` | Admin JWT | [Logs](../Ecom.API/Controllers/AdminMonitoringController.cs#L17) |  |

## AdminOrders

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/order` | Admin JWT | [GetOrders](../Ecom.API/Controllers/AdminOrdersController.cs#L24) |  |
| GET | `/api/admin/order/{id}` | Admin JWT | [GetOrder](../Ecom.API/Controllers/AdminOrdersController.cs#L46) |  |
| GET | `/api/admin/order/number/{orderNumber}` | Admin JWT | [GetOrderByNumber](../Ecom.API/Controllers/AdminOrdersController.cs#L57) |  |
| GET | `/api/admin/order/user/{userId}` | Admin JWT | [GetUserOrders](../Ecom.API/Controllers/AdminOrdersController.cs#L68) |  |
| PUT | `/api/admin/order/{id}/status` | Admin JWT | [UpdateOrderStatus](../Ecom.API/Controllers/AdminOrdersController.cs#L87) |  |
| PUT | `/api/admin/order/{id}/cancel` | Admin JWT | [CancelOrder](../Ecom.API/Controllers/AdminOrdersController.cs#L115) |  |
| GET | `/api/admin/order/statistics` | Admin JWT | [GetOrderStatistics](../Ecom.API/Controllers/AdminOrdersController.cs#L137) |  |
| GET | `/api/admin/order/sales` | Admin JWT | [GetTotalSales](../Ecom.API/Controllers/AdminOrdersController.cs#L144) |  |
| POST | `/api/admin/order/{id}/payment` | Admin JWT | [ProcessPayment](../Ecom.API/Controllers/AdminOrdersController.cs#L151) |  |
| GET | `/api/admin/order/{id}/transactions` | Admin JWT | [GetOrderTransactions](../Ecom.API/Controllers/AdminOrdersController.cs#L175) |  |

## AdminProducts

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/products` | Admin JWT | [GetProducts](../Ecom.API/Controllers/AdminProductsController.cs#L21) |  |
| GET | `/api/admin/products/{id}` | Admin JWT | [GetProduct](../Ecom.API/Controllers/AdminProductsController.cs#L51) |  |
| GET | `/api/admin/products/featured` | Admin JWT | [GetFeaturedProducts](../Ecom.API/Controllers/AdminProductsController.cs#L62) |  |
| GET | `/api/admin/products/category/{categoryId}` | Admin JWT | [GetProductsByCategory](../Ecom.API/Controllers/AdminProductsController.cs#L80) |  |
| GET | `/api/admin/products/subcategory/{subCategoryId}` | Admin JWT | [GetProductsBySubCategory](../Ecom.API/Controllers/AdminProductsController.cs#L99) |  |
| GET | `/api/admin/products/{id}/related` | Admin JWT | [GetRelatedProducts](../Ecom.API/Controllers/AdminProductsController.cs#L118) |  |
| POST | `/api/admin/products` | Admin JWT | [CreateProduct](../Ecom.API/Controllers/AdminProductsController.cs#L137) |  |
| PUT | `/api/admin/products/{id}` | Admin JWT | [UpdateProduct](../Ecom.API/Controllers/AdminProductsController.cs#L157) |  |
| DELETE | `/api/admin/products/{id}` | Admin JWT | [DeleteProduct](../Ecom.API/Controllers/AdminProductsController.cs#L184) |  |
| GET | `/api/admin/products/{id}/ratings` | Admin JWT | [GetProductRatings](../Ecom.API/Controllers/AdminProductsController.cs#L202) |  |
| POST | `/api/admin/products/{id}/ratings` | Admin JWT | [AddProductRating](../Ecom.API/Controllers/AdminProductsController.cs#L221) |  |
| GET | `/api/admin/products/in-stock` | Admin JWT | [GetInStockProducts](../Ecom.API/Controllers/AdminProductsController.cs#L246) |  |
| GET | `/api/admin/products/out-of-stock` | Admin JWT | [GetOutOfStockProducts](../Ecom.API/Controllers/AdminProductsController.cs#L264) |  |
| GET | `/api/admin/products/low-stock` | Admin JWT | [GetLowStockProducts](../Ecom.API/Controllers/AdminProductsController.cs#L282) |  |
| PUT | `/api/admin/products/{id}/stock` | Admin JWT | [UpdateProductStock](../Ecom.API/Controllers/AdminProductsController.cs#L301) |  |
| PUT | `/api/admin/products/{id}/stock/increase` | Admin JWT | [IncreaseProductStock](../Ecom.API/Controllers/AdminProductsController.cs#L337) |  |
| PUT | `/api/admin/products/{id}/stock/reduce` | Admin JWT | [ReduceProductStock](../Ecom.API/Controllers/AdminProductsController.cs#L373) |  |
| PUT | `/api/admin/products/{id}/stock/status` | Admin JWT | [SetProductStockStatus](../Ecom.API/Controllers/AdminProductsController.cs#L409) |  |
| PUT | `/api/admin/products/{id}/toggle-active` | Admin JWT | [ToggleProductActive](../Ecom.API/Controllers/AdminProductsController.cs#L445) |  |

## AdminShipping

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/shipping` | Admin JWT | [Get](../Ecom.API/Controllers/AdminShippingController.cs#L15) |  |
| PUT | `/api/admin/shipping` | Admin JWT | [Update](../Ecom.API/Controllers/AdminShippingController.cs#L19) |  |

## AdminTransaction

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| POST | `/api/admin/transaction/sadad/reconcile` | Admin JWT | [ReconcileSadadInvoices](../Ecom.API/Controllers/AdminTransactionController.cs#L33) |  |
| GET | `/api/admin/transaction` | Admin JWT | [GetTransactions](../Ecom.API/Controllers/AdminTransactionController.cs#L62) |  |
| GET | `/api/admin/transaction/{id}` | Admin JWT | [GetTransaction](../Ecom.API/Controllers/AdminTransactionController.cs#L79) |  |
| GET | `/api/admin/transaction/reference/{reference}` | Admin JWT | [GetTransactionByReference](../Ecom.API/Controllers/AdminTransactionController.cs#L100) |  |
| GET | `/api/admin/transaction/order/{orderId}` | Admin JWT | [GetTransactionsByOrder](../Ecom.API/Controllers/AdminTransactionController.cs#L121) |  |
| GET | `/api/admin/transaction/order/{orderId}/latest` | Admin JWT | [GetLatestTransactionByOrder](../Ecom.API/Controllers/AdminTransactionController.cs#L141) |  |
| POST | `/api/admin/transaction` | Admin JWT | [CreateTransaction](../Ecom.API/Controllers/AdminTransactionController.cs#L162) |  |
| PUT | `/api/admin/transaction/{id}` | Admin JWT | [UpdateTransaction](../Ecom.API/Controllers/AdminTransactionController.cs#L176) |  |
| DELETE | `/api/admin/transaction/{id}` | Admin JWT | [DeleteTransaction](../Ecom.API/Controllers/AdminTransactionController.cs#L207) |  |
| POST | `/api/admin/transaction/process-payment` | Admin JWT | [ProcessPayment](../Ecom.API/Controllers/AdminTransactionController.cs#L228) |  |
| POST | `/api/admin/transaction/refund` | Admin JWT | [ProcessRefund](../Ecom.API/Controllers/AdminTransactionController.cs#L242) | Local refund state. See AS-05. |
| PUT | `/api/admin/transaction/{id}/status` | Admin JWT | [UpdateTransactionStatus](../Ecom.API/Controllers/AdminTransactionController.cs#L272) |  |
| PUT | `/api/admin/transaction/{id}/complete` | Admin JWT | [MarkTransactionAsCompleted](../Ecom.API/Controllers/AdminTransactionController.cs#L301) |  |
| PUT | `/api/admin/transaction/{id}/fail` | Admin JWT | [MarkTransactionAsFailed](../Ecom.API/Controllers/AdminTransactionController.cs#L323) |  |
| PUT | `/api/admin/transaction/{id}/pending` | Admin JWT | [MarkTransactionAsPending](../Ecom.API/Controllers/AdminTransactionController.cs#L350) |  |
| GET | `/api/admin/transaction/analytics` | Admin JWT | [GetTransactionAnalytics](../Ecom.API/Controllers/AdminTransactionController.cs#L372) |  |
| GET | `/api/admin/transaction/analytics/order/{orderId}` | Admin JWT | [GetOrderTransactionAnalytics](../Ecom.API/Controllers/AdminTransactionController.cs#L391) |  |
| GET | `/api/admin/transaction/revenue/payment-methods` | Admin JWT | [GetRevenueByPaymentMethod](../Ecom.API/Controllers/AdminTransactionController.cs#L408) |  |
| GET | `/api/admin/transaction/trends` | Admin JWT | [GetTransactionTrends](../Ecom.API/Controllers/AdminTransactionController.cs#L428) |  |
| GET | `/api/admin/transaction/refunded` | Admin JWT | [GetRefundedTransactions](../Ecom.API/Controllers/AdminTransactionController.cs#L448) |  |
| GET | `/api/admin/transaction/refunded/total` | Admin JWT | [GetTotalRefundedAmount](../Ecom.API/Controllers/AdminTransactionController.cs#L467) |  |
| GET | `/api/admin/transaction/{id}/can-refund` | Admin JWT | [CanRefundTransaction](../Ecom.API/Controllers/AdminTransactionController.cs#L486) |  |
| GET | `/api/admin/transaction/search` | Admin JWT | [SearchTransactions](../Ecom.API/Controllers/AdminTransactionController.cs#L503) |  |
| GET | `/api/admin/transaction/status/{status}` | Admin JWT | [GetTransactionsByStatus](../Ecom.API/Controllers/AdminTransactionController.cs#L527) |  |
| GET | `/api/admin/transaction/payment-method/{paymentMethod}` | Admin JWT | [GetTransactionsByPaymentMethod](../Ecom.API/Controllers/AdminTransactionController.cs#L547) |  |
| GET | `/api/admin/transaction/export/csv` | Admin JWT | [ExportTransactionsToCsv](../Ecom.API/Controllers/AdminTransactionController.cs#L567) |  |
| GET | `/api/admin/transaction/export/excel` | Admin JWT | [ExportTransactionsToExcel](../Ecom.API/Controllers/AdminTransactionController.cs#L586) |  |

## AdminUserManager

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/user-manager/users` | Admin JWT | [GetUsers](../Ecom.API/Controllers/AdminUserManagerController.cs#L32) |  |
| GET | `/api/admin/user-manager/users/{userId}` | Admin JWT | [GetUser](../Ecom.API/Controllers/AdminUserManagerController.cs#L55) |  |
| POST | `/api/admin/user-manager/users/block` | Admin JWT | [BlockUser](../Ecom.API/Controllers/AdminUserManagerController.cs#L78) |  |
| POST | `/api/admin/user-manager/users/unblock` | Admin JWT | [UnblockUser](../Ecom.API/Controllers/AdminUserManagerController.cs#L106) |  |
| POST | `/api/admin/user-manager/users/change-password` | Admin JWT | [ChangeUserPassword](../Ecom.API/Controllers/AdminUserManagerController.cs#L134) |  |
| POST | `/api/admin/user-manager/users/change-email` | Admin JWT | [ChangeUserEmail](../Ecom.API/Controllers/AdminUserManagerController.cs#L162) |  |
| POST | `/api/admin/user-manager/users/send-email-confirmation` | Admin JWT | [SendEmailConfirmation](../Ecom.API/Controllers/AdminUserManagerController.cs#L190) |  |
| POST | `/api/admin/user-manager/users/{userId}/confirm-email` | Admin JWT | [ConfirmUserEmail](../Ecom.API/Controllers/AdminUserManagerController.cs#L218) |  |
| POST | `/api/admin/user-manager/users/{userId}/reset-password` | Admin JWT | [ResetUserPassword](../Ecom.API/Controllers/AdminUserManagerController.cs#L246) |  |
| GET | `/api/admin/user-manager/addresses` | Admin JWT | [GetUserAddresses](../Ecom.API/Controllers/AdminUserManagerController.cs#L273) |  |

## AIShopping

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| POST | `/api/ai-shopping/sessions` | Public | [Session](../Ecom.API/Controllers/AIShoppingController.cs#L21) | Issues a guest-session capability when enabled. |
| POST | `/api/ai-shopping/chat` | JWT or AI guest capability | [Chat](../Ecom.API/Controllers/AIShoppingController.cs#L25) | Feature gate, rate limits and owner scope. |
| GET | `/api/ai-shopping/conversations/{id:guid}` | JWT or AI guest capability | [Conversation](../Ecom.API/Controllers/AIShoppingController.cs#L32) | Feature gate, rate limits and owner scope. |
| POST | `/api/ai-shopping/conversations/{id:guid}/recalculate` | JWT or AI guest capability | [Recalculate](../Ecom.API/Controllers/AIShoppingController.cs#L36) | Feature gate, rate limits and owner scope. |
| POST | `/api/ai-shopping/conversations/{id:guid}/recommendations/{recommendationId:guid}/confirm` | JWT or AI guest capability | [Confirm](../Ecom.API/Controllers/AIShoppingController.cs#L40) | Feature gate, rate limits and owner scope. |
| POST | `/api/ai-shopping/index/batch` | Admin JWT | [IndexBatch](../Ecom.API/Controllers/AIShoppingController.cs#L49) | Feature gate and request filter also apply. |
| POST | `/api/ai-shopping/conversations/{id:guid}/recommendations/{recommendationId:guid}/prepare-cart` | JWT or AI guest capability | [PrepareBrowserCart](../Ecom.API/Controllers/AIShoppingController.cs#L55) | Feature gate, rate limits and owner scope. |

## Auth

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Auth/isAuth` | Public | [isAuth](../Ecom.API/Controllers/AuthController.cs#L69) | Checks whether a JWT principal exists. |
| GET | `/api/Auth/isAdmin` | Admin JWT | [Get](../Ecom.API/Controllers/AuthController.cs#L72) |  |
| POST | `/api/Auth/register` | Public | [Register](../Ecom.API/Controllers/AuthController.cs#L79) |  |
| POST | `/api/Auth/login` | Public | [Login](../Ecom.API/Controllers/AuthController.cs#L110) |  |
| POST | `/api/Auth/verify` | Public | [VerifyAccount](../Ecom.API/Controllers/AuthController.cs#L172) |  |
| POST | `/api/Auth/resend-verification` | Public | [ResendVerification](../Ecom.API/Controllers/AuthController.cs#L203) |  |
| POST | `/api/Auth/change-password` | JWT | [ChangePassword](../Ecom.API/Controllers/AuthController.cs#L234) |  |
| POST | `/api/Auth/forgot-password` | Public | [ForgotPassword](../Ecom.API/Controllers/AuthController.cs#L276) |  |
| POST | `/api/Auth/reset-password` | Public | [ResetPassword](../Ecom.API/Controllers/AuthController.cs#L298) |  |
| POST | `/api/Auth/logout` | JWT | [Logout](../Ecom.API/Controllers/AuthController.cs#L329) |  |
| GET | `/api/Auth/me` | JWT | [GetCurrentUser](../Ecom.API/Controllers/AuthController.cs#L352) |  |
| PUT | `/api/Auth/me/username` | JWT | [UpdateUsername](../Ecom.API/Controllers/AuthController.cs#L384) |  |
| PUT | `/api/Auth/me/phone` | JWT | [UpdatePhoneNumber](../Ecom.API/Controllers/AuthController.cs#L425) |  |
| POST | `/api/Auth/refresh-token` | Public | [RefreshToken](../Ecom.API/Controllers/AuthController.cs#L465) | Refresh credential in JSON body. |
| GET | `/api/Auth/addresses` | JWT | [GetUserAddresses](../Ecom.API/Controllers/AuthController.cs#L554) |  |
| GET | `/api/Auth/addresses/{addressId}` | JWT | [GetAddressById](../Ecom.API/Controllers/AuthController.cs#L589) |  |
| POST | `/api/Auth/addresses` | JWT | [CreateAddress](../Ecom.API/Controllers/AuthController.cs#L633) |  |
| PUT | `/api/Auth/addresses` | JWT | [UpdateAddress](../Ecom.API/Controllers/AuthController.cs#L678) |  |
| DELETE | `/api/Auth/addresses/{addressId}` | JWT | [DeleteAddress](../Ecom.API/Controllers/AuthController.cs#L731) |  |
| POST | `/api/Auth/contact` | Public | [Contact](../Ecom.API/Controllers/AuthController.cs#L827) |  |

## Carousels

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Carousels` | Public | [GetCarousels](../Ecom.API/Controllers/CarouselsController.cs#L21) |  |
| GET | `/api/Carousels/{id}` | Public | [GetCarousel](../Ecom.API/Controllers/CarouselsController.cs#L28) |  |
| GET | `/api/Carousels/customer-satisfaction` | Public | [GetCustomerSatisfactionPercentage](../Ecom.API/Controllers/CarouselsController.cs#L42) |  |
| GET | `/api/Carousels/latest-third-reviews` | Public | [GetLatestThirdReviews](../Ecom.API/Controllers/CarouselsController.cs#L52) |  |
| GET | `/api/Carousels/categories-with-images` | Public | [GetCategoriesWithImages](../Ecom.API/Controllers/CarouselsController.cs#L59) |  |

## Categories

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Categories` | Public | [GetCategories](../Ecom.API/Controllers/CategoriesController.cs#L19) |  |
| GET | `/api/Categories/{id}` | Public | [GetCategory](../Ecom.API/Controllers/CategoriesController.cs#L29) |  |
| GET | `/api/Categories/subcategories` | Public | [GetSubCategories](../Ecom.API/Controllers/CategoriesController.cs#L41) |  |
| GET | `/api/Categories/{id}/subcategories` | Public | [GetSubCategories](../Ecom.API/Controllers/CategoriesController.cs#L49) |  |
| GET | `/api/Categories/subcategories/{id}` | Public | [GetSubCategory](../Ecom.API/Controllers/CategoriesController.cs#L57) |  |

## Coupons

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Coupons` | Public | [GetCoupons](../Ecom.API/Controllers/CouponsController.cs#L19) |  |
| GET | `/api/Coupons/active` | Public | [GetActiveCoupons](../Ecom.API/Controllers/CouponsController.cs#L26) |  |
| GET | `/api/Coupons/{id}` | Public | [GetCoupon](../Ecom.API/Controllers/CouponsController.cs#L33) |  |
| GET | `/api/Coupons/code/{code}` | Public | [GetCouponByCode](../Ecom.API/Controllers/CouponsController.cs#L44) |  |
| POST | `/api/Coupons/validate` | Public | [ValidateCoupon](../Ecom.API/Controllers/CouponsController.cs#L55) |  |
| GET | `/api/Coupons/my-coupons` | JWT | [GetMyCoupons](../Ecom.API/Controllers/CouponsController.cs#L74) | Filters by caller ID from JWT. |
| POST | `/api/Coupons` | Admin JWT | [CreateCoupon](../Ecom.API/Controllers/CouponsController.cs#L88) |  |
| PUT | `/api/Coupons/{id}` | Admin JWT | [UpdateCoupon](../Ecom.API/Controllers/CouponsController.cs#L112) |  |
| DELETE | `/api/Coupons/{id}` | Admin JWT | [DeleteCoupon](../Ecom.API/Controllers/CouponsController.cs#L141) |  |
| PUT | `/api/Coupons/{id}/activate` | Admin JWT | [ActivateCoupon](../Ecom.API/Controllers/CouponsController.cs#L160) |  |
| PUT | `/api/Coupons/{id}/deactivate` | Admin JWT | [DeactivateCoupon](../Ecom.API/Controllers/CouponsController.cs#L179) |  |
| GET | `/api/Coupons/expired` | Admin JWT | [GetExpiredCoupons](../Ecom.API/Controllers/CouponsController.cs#L198) |  |
| POST | `/api/Coupons/cleanup-expired` | Admin JWT | [CleanupExpiredCoupons](../Ecom.API/Controllers/CouponsController.cs#L206) |  |

## Orders

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Orders/{id}` | Public | [GetOrder](../Ecom.API/Controllers/OrdersController.cs#L31) | Owner JWT, Admin JWT, or order-scoped guest capability. |
| GET | `/api/Orders/number/{orderNumber}` | Public | [GetOrderByNumber](../Ecom.API/Controllers/OrdersController.cs#L47) | Owner JWT, Admin JWT, or order-scoped guest capability. |
| GET | `/api/Orders/my-orders` | JWT | [GetMyOrders](../Ecom.API/Controllers/OrdersController.cs#L62) |  |
| POST | `/api/Orders` | Public | [CreateOrder](../Ecom.API/Controllers/OrdersController.cs#L76) | Guest checkout or JWT. Accepts an idempotency key. |
| GET | `/api/Orders/{id}/payment-status` | Public | [GetPaymentStatus](../Ecom.API/Controllers/OrdersController.cs#L111) | Owner, Admin, or guest capability. Optional Sadad verification. |
| POST | `/api/Orders/{id}/payment-attempts` | Public | [RetryPayment](../Ecom.API/Controllers/OrdersController.cs#L136) | Owner, Admin, or guest capability. Requires idempotency key. |
| PUT | `/api/Orders/{id}/cancel` | Public | [CancelOrder](../Ecom.API/Controllers/OrdersController.cs#L155) | Owner, Admin, or guest capability. |
| POST | `/api/Orders/{id}/payment` | Public | [ProcessPayment](../Ecom.API/Controllers/OrdersController.cs#L187) | Legacy action returns 410 Gone. |
| GET | `/api/Orders/{id}/transactions` | Public | [GetOrderTransactions](../Ecom.API/Controllers/OrdersController.cs#L195) | Owner, Admin, or guest capability. |
| GET | `/api/Orders/{id}/invoice-payment` | Public | [GetInvoicePaymentData](../Ecom.API/Controllers/OrdersController.cs#L205) | Owner, Admin, or guest capability. |
| POST | `/api/Orders/{id}/payment-link` | Public | [GetOrderPaymentLink](../Ecom.API/Controllers/OrdersController.cs#L224) | Owner, Admin, or guest capability. May rotate guest capability. |
| POST | `/api/Orders/webhook/sadad-paid` | Public | [SadadPaidWebhook](../Ecom.API/Controllers/OrdersController.cs#L264) | Public trigger for provider-side verification. |
| GET | `/api/Orders/payment-callback` | Public | [PaymentCallback](../Ecom.API/Controllers/OrdersController.cs#L323) | Public trigger. Returns a verification outcome. |

## Products

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Products` | Public | [GetProducts](../Ecom.API/Controllers/ProductsController.cs#L20) |  |
| GET | `/api/Products/{id}` | Public | [GetProduct](../Ecom.API/Controllers/ProductsController.cs#L54) |  |
| GET | `/api/Products/featured` | Public | [GetFeaturedProducts](../Ecom.API/Controllers/ProductsController.cs#L65) |  |
| GET | `/api/Products/category/{categoryId}` | Public | [GetProductsByCategory](../Ecom.API/Controllers/ProductsController.cs#L72) |  |
| GET | `/api/Products/subcategory/{subCategoryId}` | Public | [GetProductsBySubCategory](../Ecom.API/Controllers/ProductsController.cs#L79) |  |
| GET | `/api/Products/subcategory/{subCategoryId}/random` | Public | [GetRandomProductsBySubCategory](../Ecom.API/Controllers/ProductsController.cs#L86) |  |
| GET | `/api/Products/{id}/related` | Public | [GetRelatedProducts](../Ecom.API/Controllers/ProductsController.cs#L103) |  |
| GET | `/api/Products/{id}/ratings` | Public | [GetProductRatings](../Ecom.API/Controllers/ProductsController.cs#L111) |  |
| GET | `/api/Products/if-rating-return-it` | JWT | [myProductRate](../Ecom.API/Controllers/ProductsController.cs#L117) |  |
| POST | `/api/Products/{id}/ratings` | Public | [AddProductRating](../Ecom.API/Controllers/ProductsController.cs#L132) | Anonymous writes allowed. See AS-10. |
| GET | `/api/Products/in-stock` | Public | [GetInStockProducts](../Ecom.API/Controllers/ProductsController.cs#L157) |  |
| GET | `/api/Products/out-of-stock` | Public | [GetOutOfStockProducts](../Ecom.API/Controllers/ProductsController.cs#L164) |  |
| GET | `/api/Products/low-stock` | Public | [GetLowStockProducts](../Ecom.API/Controllers/ProductsController.cs#L171) |  |
| GET | `/api/Products/{id}/stock` | Public | [GetProductStock](../Ecom.API/Controllers/ProductsController.cs#L178) |  |

## Shipping

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/shipping` | Public | [Get](../Ecom.API/Controllers/ShippingController.cs#L14) |  |

## Transactions

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/Transactions/my` | JWT | [GetMyTransactions](../Ecom.API/Controllers/TransactionsController.cs#L25) | Filters by caller ID from JWT. |

## Visitors

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| POST | `/api/visitors/track` | Public | [Track](../Ecom.API/Controllers/VisitorsController.cs#L22) |  |

## WhatsApp

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/whatsapp/invoices/{id:int}/document` | Public | [Document](../Ecom.API/Controllers/WhatsAppController.cs#L21) | Invoice-scoped query capability with an expiration. |
| POST | `/api/whatsapp/webhook` | Public | [Webhook](../Ecom.API/Controllers/WhatsAppController.cs#L38) | Configured webhook query secret and application checks. |
| GET | `/api/whatsapp/admin/invoices` | Admin JWT | [Status](../Ecom.API/Controllers/WhatsAppController.cs#L124) |  |
| POST | `/api/whatsapp/admin/invoices/{id:int}/retry-preparation` | Admin JWT | [RetryPreparation](../Ecom.API/Controllers/WhatsAppController.cs#L146) |  |
| POST | `/api/whatsapp/admin/invoices/{id:int}/reconcile` | Admin JWT | [Reconcile](../Ecom.API/Controllers/WhatsAppController.cs#L157) |  |

## WhatsAppPreferences

| Method | Route | Access | Action | Additional control |
| --- | --- | --- | --- | --- |
| GET | `/api/account/whatsapp/preferences` | JWT | [Get](../Ecom.API/Controllers/WhatsAppPreferencesController.cs#L14) |  |
| POST | `/api/account/whatsapp/phone-verification` | JWT | [RequestVerification](../Ecom.API/Controllers/WhatsAppPreferencesController.cs#L21) |  |
| POST | `/api/account/whatsapp/phone-verification/confirm` | JWT | [ConfirmVerification](../Ecom.API/Controllers/WhatsAppPreferencesController.cs#L35) |  |
| PUT | `/api/account/whatsapp/preferences` | JWT | [Update](../Ecom.API/Controllers/WhatsAppPreferencesController.cs#L43) |  |

## Host routes and static content

| Method | Route | Access | Behavior |
| --- | --- | --- | --- |
| GET | `/admin/monitoring` | Public | Dashboard shell. Monitoring data loads through Admin APIs. [Mapping](../Ecom.API/Program.cs#L512) |
| GET, HEAD | `/monitoring/index.html`, `/monitoring/app.js`, `/monitoring/style.css` | Public | Files served by static-file middleware. [Mapping](../Ecom.API/Program.cs#L513) |
| Health middleware | `/health` | Public | Reports database health name, status and elapsed time. [Mapping](../Ecom.API/Program.cs#L528) |
| GET | `/openapi/v1.json`, `/swagger`, `/swagger/index.html`, `/swagger/v1/swagger.json` and Swagger UI assets | Development only, public in that environment | Framework and Swagger registrations. The audit inspected route setup in source. [Mapping](../Ecom.API/Program.cs#L495) |


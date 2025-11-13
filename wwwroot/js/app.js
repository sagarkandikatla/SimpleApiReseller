// AngularJS App Configuration
var app = angular.module('apiResellerApp', ['ngRoute'])
    .config(['$routeProvider', '$locationProvider', function ($routeProvider, $locationProvider) {

        // Enable HTML5 mode with hashPrefix
        $locationProvider.hashPrefix('!');

        $routeProvider
            .when('/login', {
                templateUrl: 'views/login.html?vc=0.0.7',
                controller: 'LoginController'
            })
            .when('/admin/dashboard', {
                templateUrl: 'views/admin/dashboard.html?vc=0.0.7',
                controller: 'AdminDashboardController',
                requireAuth: true,
                requireRole: 'Admin'
            })
            .when('/admin/clients', {
                templateUrl: 'views/admin/clients.html?vc=0.0.7',
                controller: 'AdminClientsController',
                requireAuth: true,
                requireRole: 'Admin'
            })
            .when('/admin/settings', {
                templateUrl: 'views/admin/settings.html?vc=0.0.7',
                controller: 'AdminSettingsController',
                requireAuth: true,
                requireRole: 'Admin'
            })
            .when('/client/dashboard', {
                templateUrl: 'views/client/dashboard.html?vc=0.0.7',
                controller: 'ClientDashboardController',
                requireAuth: true,
                requireRole: 'Client'
            })
            .when('/client/credits', {
                templateUrl: 'views/client/credits.html?vc=0.0.7',
                controller: 'ClientCreditsController',
                requireAuth: true,
                requireRole: 'Client'
            })
            .when('/client/statistics', {
                templateUrl: 'views/client/statistics.html?vc=0.0.7',
                controller: 'ClientStatisticsController',
                requireAuth: true,
                requireRole: 'Client'
            })
            .when('/documentation', {
                templateUrl: 'views/documentation.html?vc=0.0.7',
                controller: 'DocumentationController',
                requireAuth: true
            })
            .when('/', {
                template: '<div>Redirecting...</div>',
                controller: 'RedirectController'
            })
            .otherwise({ redirectTo: '/' });

    }])
    .run(['$rootScope', '$location', '$timeout', 'AuthService', function ($rootScope, $location, $timeout, AuthService) {
        // Global variables
        $rootScope.loading = false;
        $rootScope.alert = {};

        // Show alert helper
        $rootScope.showAlert = function (message, type) {
            type = type || 'info';
            $rootScope.alert = { message: message, type: type };

            // Auto-dismiss after 5 seconds
            $timeout(function () {
                $rootScope.clearAlert();
            }, 5000);
        };

        // Clear alert helper
        $rootScope.clearAlert = function () {
            $rootScope.alert = {};
        };

        // Route change start
        $rootScope.$on('$routeChangeStart', function (event, next, current) {
            $rootScope.loading = true;
            $rootScope.clearAlert();

            if (next.requireAuth && !AuthService.isAuthenticated()) {
                event.preventDefault();
                $location.path('/login');
                return;
            }

            if (next.requireRole) {
                var user = AuthService.getCurrentUser();
                if (!user || user.role !== next.requireRole) {
                    event.preventDefault();
                    $rootScope.showAlert('Access denied. Insufficient permissions.', 'danger');
                    $location.path('/');
                    return;
                }
            }
        });

        // Route change success
        $rootScope.$on('$routeChangeSuccess', function () {
            $rootScope.loading = false;
        });

        // Route change error
        $rootScope.$on('$routeChangeError', function (event, current, previous, rejection) {
            $rootScope.loading = false;
            console.error('Route Change Error:', rejection);
            $rootScope.showAlert('Failed to load page', 'danger');
        });

        // HTTP errors
        $rootScope.$on('httpError', function (event, error) {
            $rootScope.loading = false;
            if (error.status === 401) {
                AuthService.logout();
                $location.path('/login');
            } else {
                $rootScope.showAlert(error.message || 'An error occurred', 'danger');
            }
        });
    }]);

// Filters - UPDATED FOR INR
app.filter('currency', function () {
    return function (amount) {
        if (amount === null || amount === undefined) return '₹0.00';

        // Format number with Indian currency format (lakhs/crores)
        var num = parseFloat(amount).toFixed(2);
        var parts = num.toString().split(".");

        // Indian number formatting: XX,XX,XXX.XX
        var lastThree = parts[0].substring(parts[0].length - 3);
        var otherNumbers = parts[0].substring(0, parts[0].length - 3);

        if (otherNumbers !== '') {
            lastThree = ',' + lastThree;
        }

        var formatted = otherNumbers.replace(/\B(?=(\d{2})+(?!\d))/g, ",") + lastThree;

        if (parts.length > 1) {
            formatted += "." + parts[1];
        }

        return '₹' + formatted;
    };
})
    .filter('dateFormat', function () {
        return function (dateString) {
            if (!dateString) return '';
            return new Date(dateString).toLocaleDateString('en-IN');
        };
    })
    .filter('dateTimeFormat', function () {
        return function (dateString) {
            if (!dateString) return '';
            return new Date(dateString).toLocaleString('en-IN');
        };
    });

// Redirect Controller
app.controller('RedirectController', ['$location', 'AuthService', function ($location, AuthService) {
    var user = AuthService.getCurrentUser();
    console.log('RedirectController: current user', user);

    if (user && user.role) {
        if (user.role === 'Admin') {
            $location.path('/admin/dashboard');
        } else {
            $location.path('/client/dashboard');
        }
    } else {
        $location.path('/login');
    }
}]);
angular.module('apiResellerApp')

    // Client Dashboard Controller
    .controller('ClientDashboardController', ['$scope', 'ApiService', 'AuthService', function ($scope, ApiService, AuthService) {
        $scope.balance = { creditBalance: 0, recentTransactions: [] };
        $scope.summary = {
            today: { requests: 0, creditsUsed: 0 },
            thisMonth: { requests: 0, creditsUsed: 0 },
            lifetime: { requests: 0, creditsUsed: 0 }
        };
        $scope.currentUser = AuthService.getCurrentUser();
        $scope.apiEndpoint = window.location.origin;

        $scope.loadDashboardData = function () {
            // FIXED: Prevent admin from calling client APIs
            if ($scope.currentUser.role === 'Admin') {
                console.log('Admin user detected, skipping client API calls');
                return;
            }

            // FIXED: Check if clientId exists
            if (!$scope.currentUser.clientId) {
                console.error('No clientId found for user');
                $scope.$parent.showAlert('Client ID not found. Please contact administrator.', 'danger');
                return;
            }

            $scope.$parent.loading = true;

            Promise.all([
                ApiService.getMyCreditBalance(),
                ApiService.getMyUsageSummary()
            ]).then(function (responses) {
                $scope.balance = responses[0].data;
                $scope.summary = responses[1].data;
                $scope.$applyAsync();
            }).catch(function (error) {
                console.error('Dashboard load error:', error);
                $scope.$parent.showAlert('Failed to load dashboard data. Please try again.', 'danger');
            }).finally(function () {
                $scope.$parent.loading = false;
                $scope.$applyAsync();
            });
        };

        $scope.refreshData = function () {
            $scope.loadDashboardData();
        };

        $scope.copyApiKey = function () {
            if ($scope.currentUser.role === 'Admin') {
                $scope.$parent.showAlert('Admin users do not have API keys', 'info');
                return;
            }

            if (!$scope.currentUser.clientId) {
                $scope.$parent.showAlert('Client ID not found', 'danger');
                return;
            }

            ApiService.get('/clients/' + $scope.currentUser.clientId)
                .then(function (response) {
                    var apiKey = response.data.apiKey;
                    navigator.clipboard.writeText(apiKey).then(function () {
                        $scope.$parent.showAlert('API Key copied to clipboard!', 'success');
                    }).catch(function (err) {
                        console.error('Copy failed:', err);
                        $scope.$parent.showAlert('Failed to copy API key', 'danger');
                    });
                })
                .catch(function (error) {
                    console.error('Failed to get API key:', error);
                    $scope.$parent.showAlert('Failed to retrieve API key', 'danger');
                });
        };

        // Only load data if user is a client
        if ($scope.currentUser.role === 'Client') {
            $scope.loadDashboardData();
        }
    }])

    // Client Credits Controller
    .controller('ClientCreditsController', ['$scope', 'ApiService', function ($scope, ApiService) {
        $scope.balance = {};
        $scope.transactions = [];
        $scope.pagination = {};
        $scope.currentPage = 1;
        $scope.pageSize = 20;
        $scope.selectedType = '';
        $scope.Math = window.Math; // ADD THIS LINE

        $scope.loadBalance = function () {
            ApiService.getMyCreditBalance()
                .then(function (response) {
                    $scope.balance = response.data;
                });
        };

        $scope.loadTransactions = function (page) {
            page = page || $scope.currentPage;

            ApiService.getMyTransactions(page, $scope.pageSize, $scope.selectedType)
                .then(function (response) {
                    $scope.transactions = response.data.transactions;
                    $scope.pagination = response.data.pagination;
                    $scope.currentPage = page;
                });
        };

        $scope.filterByType = function (type) {
            $scope.selectedType = type;
            $scope.currentPage = 1;
            $scope.loadTransactions(1);
        };

        $scope.getPages = function () {
            if (!$scope.pagination.totalPages) return [];

            var pages = [];
            var start = Math.max(1, $scope.currentPage - 2);
            var end = Math.min($scope.pagination.totalPages, $scope.currentPage + 2);

            for (var i = start; i <= end; i++) {
                pages.push(i);
            }
            return pages;
        };

        // Load initial data
        $scope.loadBalance();
        $scope.loadTransactions();
    }])

    // Client Statistics Controller
    .controller('ClientStatisticsController', ['$scope', 'ApiService', function ($scope, ApiService) {
        $scope.statistics = {};
        $scope.days = 30;
        $scope.chart = null;

        $scope.loadStatistics = function () {
            $scope.$parent.loading = true;

            ApiService.getMyStatistics($scope.days)
                .then(function (response) {
                    $scope.statistics = response.data;
                    $scope.createChart();
                    $scope.$parent.loading = false;
                })
                .catch(function (error) {
                    $scope.$parent.loading = false;
                    $scope.$parent.showAlert('Failed to load statistics', 'danger');
                });
        };

        $scope.changePeriod = function (days) {
            $scope.days = days;
            $scope.loadStatistics();
        };

        $scope.createChart = function () {
            if (!$scope.statistics.dailyStats || $scope.statistics.dailyStats.length === 0) return;

            // Wait for DOM to be ready
            setTimeout(function () {
                var ctx = document.getElementById('requestChart');
                if (!ctx) return;

                // Destroy existing chart
                if ($scope.chart) {
                    $scope.chart.destroy();
                }

                var labels = $scope.statistics.dailyStats.map(function (stat) {
                    return new Date(stat.requestDate).toLocaleDateString();
                }).reverse();

                var requestData = $scope.statistics.dailyStats.map(function (stat) {
                    return stat.totalRequests;
                }).reverse();

                var successData = $scope.statistics.dailyStats.map(function (stat) {
                    return stat.successCount;
                }).reverse();

                $scope.chart = new Chart(ctx, {
                    type: 'line',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Total Requests',
                            data: requestData,
                            borderColor: 'rgb(75, 192, 192)',
                            backgroundColor: 'rgba(75, 192, 192, 0.1)',
                            tension: 0.1
                        }, {
                            label: 'Successful Requests',
                            data: successData,
                            borderColor: 'rgb(54, 162, 235)',
                            backgroundColor: 'rgba(54, 162, 235, 0.1)',
                            tension: 0.1
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: {
                            y: {
                                beginAtZero: true
                            }
                        }
                    }
                });

                // Create success rate chart
                var successCtx = document.getElementById('successChart');
                if (successCtx && $scope.statistics.summary) {
                    var successRate = $scope.statistics.summary.successRate;
                    var failureRate = 100 - successRate;

                    new Chart(successCtx, {
                        type: 'doughnut',
                        data: {
                            labels: ['Success', 'Failed'],
                            datasets: [{
                                data: [successRate, failureRate],
                                backgroundColor: ['#28a745', '#dc3545'],
                                borderWidth: 0
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {
                                legend: {
                                    position: 'bottom'
                                }
                            }
                        }
                    });
                }
            }, 100);
        };

        // Load initial data
        $scope.loadStatistics();
    }]);

// Register controller with the correct module name
angular.module('apiResellerApp')
  .controller('ClientController', ['$scope', function($scope) {
    // Controller logic here
    // Example:
    $scope.message = "Hello from ClientController!";
  }]);
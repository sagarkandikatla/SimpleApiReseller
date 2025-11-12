angular.module('apiResellerApp')

    // Admin Dashboard Controller
    .controller('AdminDashboardController', ['$scope', 'ApiService', function ($scope, ApiService) {
        $scope.dashboardStats = {};
        $scope.systemSettings = {};
        $scope.recentClients = [];
        $scope.allClients = [];
        $scope.lowCreditClients = [];
        $scope.rechargeForm = {};
        $scope.rechargeLoading = false;

        $scope.loadDashboardData = function () {
            $scope.$parent.loading = true;

            Promise.all([
                ApiService.getClients(),
                ApiService.getSystemSettings()
            ]).then(function (responses) {
                var clients = responses[0].data;
                var settings = responses[1].data;

                $scope.allClients = clients;
                $scope.recentClients = clients;
                $scope.systemSettings = settings;

                // Calculate dashboard stats
                $scope.dashboardStats = {
                    totalClients: clients.length,
                    activeClients: clients.filter(function (c) { return c.isActive; }).length,
                    totalCredits: clients.reduce(function (sum, c) { return sum + c.creditBalance; }, 0)
                };

                // Find low credit clients (less than $5)
                $scope.lowCreditClients = clients.filter(function (c) {
                    return c.isActive && c.creditBalance < 5;
                });

                $scope.$apply();
                $scope.$parent.loading = false;
            }).catch(function (error) {
                $scope.$parent.loading = false;
                $scope.$parent.showAlert('Failed to load dashboard data', 'danger');
            });
        };

        $scope.refreshData = function () {
            $scope.loadDashboardData();
        };

        $scope.viewClient = function (clientId) {
            window.location.href = '#!/admin/clients?id=' + clientId;
        };

        $scope.rechargeClient = function (client) {
            $scope.rechargeForm = {
                clientId: client.id,
                amount: null,
                description: 'Admin recharge'
            };
            var modal = new bootstrap.Modal(document.getElementById('rechargeModal'));
            modal.show();
        };

        $scope.showRechargeModal = function () {
            $scope.rechargeForm = {
                clientId: '',
                amount: null,
                description: 'Bulk recharge'
            };
            var modal = new bootstrap.Modal(document.getElementById('rechargeModal'));
            modal.show();
        };

        $scope.processRecharge = function () {
            if (!$scope.rechargeForm.clientId || !$scope.rechargeForm.amount) return;

            $scope.rechargeLoading = true;

            ApiService.rechargeCredits($scope.rechargeForm.clientId, {
                amount: parseFloat($scope.rechargeForm.amount),
                description: $scope.rechargeForm.description
            }).then(function (response) {
                $scope.rechargeLoading = false;
                $scope.$parent.showAlert('Credits recharged successfully!', 'success');

                // Close modal
                var modal = bootstrap.Modal.getInstance(document.getElementById('rechargeModal'));
                modal.hide();

                // Refresh data
                $scope.loadDashboardData();
            }).catch(function (error) {
                $scope.rechargeLoading = false;
                $scope.$parent.showAlert('Failed to recharge credits', 'danger');
            });
        };

        $scope.exportData = function () {
            // Simple CSV export
            var csvContent = "data:text/csv;charset=utf-8,";
            csvContent += "Username,Email,Company,Credits,Status,Created\n";

            $scope.allClients.forEach(function (client) {
                csvContent += [
                    client.username,
                    client.email,
                    client.companyName || '',
                    client.creditBalance,
                    client.isActive ? 'Active' : 'Inactive',
                    new Date(client.createdAt).toLocaleDateString()
                ].join(",") + "\n";
            });

            var encodedUri = encodeURI(csvContent);
            var link = document.createElement("a");
            link.setAttribute("href", encodedUri);
            link.setAttribute("download", "clients_export.csv");
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            $scope.$parent.showAlert('Data exported successfully!', 'success');
        };

        // Load initial data
        $scope.loadDashboardData();
    }])

    // Admin Clients Controller
    .controller('AdminClientsController', ['$scope', '$location', 'ApiService', function ($scope, $location, ApiService) {
        $scope.clients = [];
        $scope.filteredClients = [];
        $scope.searchTerm = '';
        $scope.statusFilter = '';
        $scope.selectedClient = null;
        $scope.clientForm = {};
        $scope.formMode = 'create'; // 'create' or 'edit'
        $scope.formLoading = false;

        $scope.loadClients = function () {
            $scope.$parent.loading = true;

            ApiService.getClients()
                .then(function (response) {
                    $scope.clients = response.data;
                    $scope.filterClients();
                    $scope.$parent.loading = false;
                })
                .catch(function (error) {
                    $scope.$parent.loading = false;
                    $scope.$parent.showAlert('Failed to load clients', 'danger');
                });
        };

        $scope.filterClients = function () {
            $scope.filteredClients = $scope.clients.filter(function (client) {
                var matchesSearch = !$scope.searchTerm ||
                    client.username.toLowerCase().includes($scope.searchTerm.toLowerCase()) ||
                    client.email.toLowerCase().includes($scope.searchTerm.toLowerCase()) ||
                    (client.companyName && client.companyName.toLowerCase().includes($scope.searchTerm.toLowerCase()));

                var matchesStatus = !$scope.statusFilter ||
                    ($scope.statusFilter === 'active' && client.isActive) ||
                    ($scope.statusFilter === 'inactive' && !client.isActive);

                return matchesSearch && matchesStatus;
            });
        };

        $scope.showCreateForm = function () {
            $scope.clientForm = {
                username: '',
                email: '',
                companyName: '',
                contactPerson: '',
                phone: '',
                address: '',
                initialCredits: 0
            };
            $scope.formMode = 'create';
            var modal = new bootstrap.Modal(document.getElementById('clientModal'));
            modal.show();
        };

        $scope.showEditForm = function (client) {
            $scope.selectedClient = client;
            $scope.clientForm = {
                email: client.email,
                companyName: client.companyName,
                contactPerson: client.contactPerson,
                phone: client.phone,
                address: client.address
            };
            $scope.formMode = 'edit';
            var modal = new bootstrap.Modal(document.getElementById('clientModal'));
            modal.show();
        };

        $scope.saveClient = function () {
            $scope.formLoading = true;

            var promise;
            if ($scope.formMode === 'create') {
                promise = ApiService.createClient($scope.clientForm);
            } else {
                promise = ApiService.updateClient($scope.selectedClient.id, $scope.clientForm);
            }

            promise.then(function (response) {
                $scope.formLoading = false;
                $scope.$parent.showAlert(
                    $scope.formMode === 'create' ? 'Client created successfully!' : 'Client updated successfully!',
                    'success'
                );

                // Show credentials for new client
                if ($scope.formMode === 'create' && response.data.credentials) {
                    $scope.showCredentials(response.data.credentials);
                }

                // Close modal and refresh
                var modal = bootstrap.Modal.getInstance(document.getElementById('clientModal'));
                modal.hide();
                $scope.loadClients();
            }).catch(function (error) {
                $scope.formLoading = false;
                $scope.$parent.showAlert('Failed to save client', 'danger');
            });
        };

        $scope.showCredentials = function (credentials) {
            $scope.newCredentials = credentials;
            var modal = new bootstrap.Modal(document.getElementById('credentialsModal'));
            modal.show();
        };

        $scope.toggleClientStatus = function (client) {
            if (confirm('Are you sure you want to ' + (client.isActive ? 'deactivate' : 'activate') + ' this client?')) {
                ApiService.toggleClientStatus(client.id)
                    .then(function (response) {
                        $scope.$parent.showAlert('Client status updated successfully!', 'success');
                        $scope.loadClients();
                    })
                    .catch(function (error) {
                        $scope.$parent.showAlert('Failed to update client status', 'danger');
                    });
            }
        };

        $scope.regenerateApiKey = function (client) {
            if (confirm('Are you sure you want to regenerate the API key for ' + client.username + '? The old key will stop working immediately.')) {
                ApiService.regenerateApiKey(client.id)
                    .then(function (response) {
                        $scope.$parent.showAlert('API key regenerated successfully!', 'success');
                        $scope.showCredentials({
                            username: client.username,
                            apiKey: response.data.newApiKey,
                            apiSecret: response.data.newApiSecret
                        });
                        $scope.loadClients();
                    })
                    .catch(function (error) {
                        $scope.$parent.showAlert('Failed to regenerate API key', 'danger');
                    });
            }
        };

        $scope.resetPassword = function (client) {
            if (confirm('Are you sure you want to reset the password for ' + client.username + '?')) {
                ApiService.resetPassword(client.id)
                    .then(function (response) {
                        $scope.$parent.showAlert('Password reset successfully!', 'success');
                        $scope.showCredentials({
                            username: client.username,
                            password: response.data.newPassword
                        });
                    })
                    .catch(function (error) {
                        $scope.$parent.showAlert('Failed to reset password', 'danger');
                    });
            }
        };

        $scope.rechargeCredits = function (client) {
            var amount = prompt('Enter amount to recharge for ' + client.username + ':');
            if (amount && !isNaN(amount) && parseFloat(amount) > 0) {
                ApiService.rechargeCredits(client.id, {
                    amount: parseFloat(amount),
                    description: 'Manual recharge by admin'
                }).then(function (response) {
                    $scope.$parent.showAlert('Credits recharged successfully!', 'success');
                    $scope.loadClients();
                }).catch(function (error) {
                    $scope.$parent.showAlert('Failed to recharge credits', 'danger');
                });
            }
        };

        $scope.viewClientStats = function (client) {
            window.open('#!/admin/client-stats/' + client.id, '_blank');
        };

        $scope.copyToClipboard = function (text, label) {
            navigator.clipboard.writeText(text).then(function () {
                $scope.$parent.showAlert(label + ' copied to clipboard!', 'success');
            });
        };

        // Load initial data
        $scope.loadClients();
    }])

    // Admin Settings Controller
    .controller('AdminSettingsController', ['$scope', 'ApiService', function ($scope, ApiService) {
        $scope.settings = {};
        $scope.settingsForm = {};
        $scope.saveLoading = false;

        $scope.loadSettings = function () {
            $scope.$parent.loading = true;

            ApiService.getSystemSettings()
                .then(function (response) {
                    $scope.settings = response.data;
                    $scope.settingsForm = {
                        requestCost: $scope.settings.requestCost
                    };
                    $scope.$parent.loading = false;
                })
                .catch(function (error) {
                    $scope.$parent.loading = false;
                    $scope.$parent.showAlert('Failed to load settings', 'danger');
                });
        };

        $scope.saveSettings = function () {
            $scope.saveLoading = true;

            ApiService.setRequestCost($scope.settingsForm.requestCost)
                .then(function (response) {
                    $scope.saveLoading = false;
                    $scope.$parent.showAlert('Settings saved successfully!', 'success');
                    $scope.loadSettings(); // Reload to get updated data
                })
                .catch(function (error) {
                    $scope.saveLoading = false;
                    $scope.$parent.showAlert('Failed to save settings', 'danger');
                });
        };

        $scope.resetToDefaults = function () {
            if (confirm('Are you sure you want to reset all settings to default values?')) {
                $scope.settingsForm.requestCost = 0.01;
            }
        };

        $scope.viewSystemLogs = function () {
            $scope.$parent.showAlert('System logs feature coming soon!', 'info');
        };

        $scope.exportSettings = function () {
            var settingsData = {
                requestCost: $scope.settings.requestCost,
                exportDate: new Date().toISOString(),
                version: '1.0.0'
            };

            var dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(settingsData, null, 2));
            var downloadAnchorNode = document.createElement('a');
            downloadAnchorNode.setAttribute("href", dataStr);
            downloadAnchorNode.setAttribute("download", "system-settings-" + new Date().toISOString().split('T')[0] + ".json");
            document.body.appendChild(downloadAnchorNode);
            downloadAnchorNode.click();
            downloadAnchorNode.remove();

            $scope.$parent.showAlert('Settings exported successfully!', 'success');
        };

        $scope.clearCache = function () {
            if (confirm('Are you sure you want to clear the system cache?')) {
                $scope.$parent.showAlert('Cache cleared successfully!', 'success');
            }
        };

        $scope.restartService = function () {
            if (confirm('Are you sure you want to restart the service? This will temporarily interrupt all API requests.')) {
                $scope.$parent.showAlert('Service restart initiated. Please wait...', 'warning');
            }
        };

        // Load initial data
        $scope.loadSettings();
    }])

    // Register controller with the correct module name
    .controller('AdminController', ['$scope', function($scope) {
        // Controller logic here
    }]);
angular.module('apiResellerApp')
    .service('ApiService', ['$http', '$rootScope', function ($http, $rootScope) {

        // HTTP interceptor for error handling
        var self = this;

        function handleError(error) {
            console.error('API Error:', error);
            $rootScope.$broadcast('httpError', {
                status: error.status,
                message: error.data?.message || 'An error occurred'
            });
            return Promise.reject(error);
        }

        // Generic HTTP methods
        this.get = function (url, params) {
            var config = params ? { params: params } : {};
            return $http.get('/api' + url, config).catch(handleError);
        };

        this.post = function (url, data) {
            return $http.post('/api' + url, data).catch(handleError);
        };

        this.put = function (url, data) {
            return $http.put('/api' + url, data).catch(handleError);
        };

        this.delete = function (url) {
            return $http.delete('/api' + url).catch(handleError);
        };

        // Admin API methods
        this.getClients = function () {
            return this.get('/clients').then(function (response) {
                return response; // Return response for promise chain
            });
        };

        this.createClient = function (clientData) {
            return this.post('/clients', clientData);
        };

        this.updateClient = function (id, clientData) {
            return this.put('/clients/' + id, clientData);
        };

        this.toggleClientStatus = function (id) {
            return this.put('/clients/' + id + '/toggle-status');
        };

        this.rechargeCredits = function (id, rechargeData) {
            return this.post('/clients/' + id + '/recharge', rechargeData);
        };

        this.regenerateApiKey = function (id) {
            return this.post('/clients/' + id + '/regenerate-api-key');
        };

        this.resetPassword = function (id) {
            return this.post('/clients/' + id + '/reset-password');
        };

        this.getClientCredits = function (id, page, pageSize) {
            return this.get('/clients/' + id + '/credits', {
                page: page || 1,
                pageSize: pageSize || 20
            });
        };

        this.getClientStatistics = function (id, days) {
            return this.get('/clients/' + id + '/statistics', {
                days: days || 30
            });
        };

        this.setRequestCost = function (requestCost) {
            return this.post('/clients/system/request-cost', { requestCost: requestCost });
        };

        this.getSystemSettings = function () {
            return this.get('/clients/system/settings');
        };

        // Client API methods
        this.getMyCreditBalance = function () {
            return this.get('/credit/my-balance');
        };

        this.getMyTransactions = function (page, pageSize, transactionType) {
            var params = {
                page: page || 1,
                pageSize: pageSize || 20
            };
            if (transactionType) {
                params.transactionType = transactionType;
            }
            return this.get('/credit/my-transactions', params);
        };

        this.getMyStatistics = function (days) {
            return this.get('/credit/statistics', {
                days: days || 30
            });
        };

        this.getMyUsageSummary = function () {
            return this.get('/credit/usage-summary');
        };

        // Dashboard methods
        this.getAdminDashboardStats = function () {
            // For admin dashboard - can combine multiple calls
            return Promise.all([
                this.getClients(),
                this.getSystemSettings()
            ]);
        };
    }]);
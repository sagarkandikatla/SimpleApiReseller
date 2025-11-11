angular.module('apiResellerApp')
    .controller('LoginController', ['$scope', '$location', 'AuthService',
        function ($scope, $location, AuthService) {

            $scope.credentials = { username: '', password: '' };
            $scope.loading = false;
            $scope.errorMessage = '';

            // ---- LOGIN FUNCTION ----
            $scope.login = function () {
                if (!$scope.credentials.username || !$scope.credentials.password) {
                    $scope.errorMessage = 'Please enter both username and password.';
                    return;
                }

                $scope.loading = true;
                $scope.errorMessage = '';

                AuthService.login($scope.credentials)
                    .then(function (result) {
                        if (result.success) {
                            var user = result.user;
                            if (user.role === 'Admin') {
                                $location.path('/admin/dashboard');
                            } else {
                                $location.path('/client/dashboard');
                            }
                        } else {
                            $scope.errorMessage = result.message;
                        }
                    })
                    .finally(function () {
                        $scope.loading = false;
                    });
            };

            // ---- FILL DEMO CREDENTIALS ----
            $scope.fillDemo = function (type) {
                if (type === 'admin') {
                    $scope.credentials.username = 'admin';
                    $scope.credentials.password = 'admin123';
                } else if (type === 'client') {
                    $scope.credentials.username = 'democlient';
                    $scope.credentials.password = 'demo123';
                }
            };

            // ---- ENTER KEY LOGIN ----
            $scope.handleKeyPress = function (event) {
                if (event.keyCode === 13) {
                    $scope.login();
                }
            };
        }]);

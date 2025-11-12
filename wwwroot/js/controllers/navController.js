angular.module('apiResellerApp')
    .controller('NavController', ['$scope', '$location', '$window', 'AuthService',
        function ($scope, $location, $window, AuthService) {

            // Initialize current user
            $scope.currentUser = AuthService.getCurrentUser();

            // Watch for user changes
            $scope.$watch(function () {
                return AuthService.getCurrentUser();
            }, function (newUser) {
                $scope.currentUser = newUser;
            }, true);

            $scope.logout = function () {
                if (confirm('Are you sure you want to logout?')) {
                    AuthService.logout();
                    $scope.$parent.showAlert('Logged out successfully', 'info');
                    $location.path('/login');
                    // Force page reload to clear any cached data
                    $window.location.reload();
                }
            };

            $scope.changePassword = function () {
                var currentPassword = prompt('Enter your current password:');
                if (!currentPassword) return;

                var newPassword = prompt('Enter your new password:');
                if (!newPassword) return;

                if (newPassword.length < 6) {
                    $scope.$parent.showAlert('Password must be at least 6 characters long', 'warning');
                    return;
                }

                var passwordData = {
                    currentPassword: currentPassword,
                    newPassword: newPassword
                };

                AuthService.changePassword(passwordData)
                    .then(function (result) {
                        if (result.success) {
                            $scope.$parent.showAlert('Password changed successfully', 'success');
                        } else {
                            $scope.$parent.showAlert(result.message, 'danger');
                        }
                    });
            };
        }]);
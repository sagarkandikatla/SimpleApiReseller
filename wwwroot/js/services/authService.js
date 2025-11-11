angular.module('apiResellerApp')
    .service('AuthService', ['$http', '$rootScope', function ($http, $rootScope) {
        var currentUser = null;
        var token = null;

        // Initialize from localStorage
        var storedToken = localStorage.getItem('token');
        var storedUser = localStorage.getItem('user');

        if (storedToken && storedUser) {
            try {
                token = storedToken;
                currentUser = JSON.parse(storedUser);
                setAuthHeader();
            } catch (e) {
                logout(); // fix: direct call to internal function
            }
        }

        function setAuthHeader() {
            if (token) {
                $http.defaults.headers.common['Authorization'] = 'Bearer ' + token;
            } else {
                delete $http.defaults.headers.common['Authorization'];
            }
        }

        function logout() {
            token = null;
            currentUser = null;
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            setAuthHeader();
            $rootScope.currentUser = null;
        }

        this.login = function (credentials) {
            return $http.post('/api/auth/login', credentials)
                .then(function (response) {
                    var data = response.data;
                    token = data.token;
                    currentUser = {
                        username: data.username,
                        role: data.role,
                        userId: data.userId,
                        clientId: data.clientId
                    };

                    localStorage.setItem('token', token);
                    localStorage.setItem('user', JSON.stringify(currentUser));
                    setAuthHeader();
                    $rootScope.currentUser = currentUser;

                    return { success: true, user: currentUser };
                })
                .catch(function (error) {
                    return {
                        success: false,
                        message: error.data?.message || 'Login failed'
                    };
                });
        };

        this.logout = logout;

        this.changePassword = function (passwordData) {
            return $http.post('/api/auth/change-password', passwordData)
                .then(() => ({ success: true }))
                .catch(error => ({
                    success: false,
                    message: error.data?.message || 'Password change failed'
                }));
        };

        this.getCurrentUser = function () {
            return currentUser;
        };

        this.isAuthenticated = function () {
            return currentUser !== null && token !== null;
        };

        this.hasRole = function (role) {
            return currentUser && currentUser.role === role;
        };

        // Initialize user in rootScope
        $rootScope.currentUser = currentUser;
    }]);

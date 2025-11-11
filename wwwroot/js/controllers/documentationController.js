angular.module('apiResellerApp')
    .controller('DocumentationController', ['$scope', 'AuthService', 'ApiService', function ($scope, AuthService, ApiService) {
        $scope.currentUser = AuthService.getCurrentUser();
        $scope.apiBaseUrl = window.location.origin;
        $scope.userApiKey = 'Loading...';
        $scope.requestCost = 0.01;

        // Load user's API key if client
        if ($scope.currentUser && $scope.currentUser.role === 'Client' && $scope.currentUser.clientId) {
            ApiService.get('/clients/' + $scope.currentUser.clientId)
                .then(function (response) {
                    $scope.userApiKey = response.data.apiKey;
                })
                .catch(function (error) {
                    $scope.userApiKey = 'Error loading API key';
                });
        }

        // Load current request cost
        ApiService.getSystemSettings()
            .then(function (response) {
                $scope.requestCost = response.data.requestCost;
            })
            .catch(function (error) {
                console.log('Could not load system settings');
            });

        $scope.copyApiKey = function () {
            navigator.clipboard.writeText($scope.userApiKey).then(function () {
                $scope.$parent.showAlert('API Key copied to clipboard!', 'success');
            });
        };

        $scope.downloadPostman = function () {
            var postmanCollection = {
                "info": {
                    "name": "API Reseller System",
                    "description": "Collection for API Reseller System endpoints",
                    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
                },
                "variable": [
                    {
                        "key": "baseUrl",
                        "value": $scope.apiBaseUrl + "/api/proxy"
                    },
                    {
                        "key": "apiKey",
                        "value": $scope.userApiKey !== 'Loading...' ? $scope.userApiKey : "YOUR_API_KEY"
                    }
                ],
                "item": [
                    {
                        "name": "Get Posts",
                        "request": {
                            "method": "GET",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                }
                            ],
                            "url": {
                                "raw": "{{baseUrl}}/posts",
                                "host": ["{{baseUrl}}"],
                                "path": ["posts"]
                            }
                        }
                    },
                    {
                        "name": "Get Single Post",
                        "request": {
                            "method": "GET",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                }
                            ],
                            "url": {
                                "raw": "{{baseUrl}}/posts/1",
                                "host": ["{{baseUrl}}"],
                                "path": ["posts", "1"]
                            }
                        }
                    },
                    {
                        "name": "Create Post",
                        "request": {
                            "method": "POST",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                },
                                {
                                    "key": "Content-Type",
                                    "value": "application/json"
                                }
                            ],
                            "body": {
                                "mode": "raw",
                                "raw": "{\n  \"title\": \"New Post\",\n  \"body\": \"This is a new post\",\n  \"userId\": 1\n}"
                            },
                            "url": {
                                "raw": "{{baseUrl}}/posts",
                                "host": ["{{baseUrl}}"],
                                "path": ["posts"]
                            }
                        }
                    },
                    {
                        "name": "Update Post",
                        "request": {
                            "method": "PUT",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                },
                                {
                                    "key": "Content-Type",
                                    "value": "application/json"
                                }
                            ],
                            "body": {
                                "mode": "raw",
                                "raw": "{\n  \"id\": 1,\n  \"title\": \"Updated Post\",\n  \"body\": \"This post has been updated\",\n  \"userId\": 1\n}"
                            },
                            "url": {
                                "raw": "{{baseUrl}}/posts/1",
                                "host": ["{{baseUrl}}"],
                                "path": ["posts", "1"]
                            }
                        }
                    },
                    {
                        "name": "Delete Post",
                        "request": {
                            "method": "DELETE",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                }
                            ],
                            "url": {
                                "raw": "{{baseUrl}}/posts/1",
                                "host": ["{{baseUrl}}"],
                                "path": ["posts", "1"]
                            }
                        }
                    },
                    {
                        "name": "Get Users",
                        "request": {
                            "method": "GET",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                }
                            ],
                            "url": {
                                "raw": "{{baseUrl}}/users",
                                "host": ["{{baseUrl}}"],
                                "path": ["users"]
                            }
                        }
                    },
                    {
                        "name": "Get Comments",
                        "request": {
                            "method": "GET",
                            "header": [
                                {
                                    "key": "X-API-Key",
                                    "value": "{{apiKey}}"
                                }
                            ],
                            "url": {
                                "raw": "{{baseUrl}}/comments",
                                "host": ["{{baseUrl}}"],
                                "path": ["comments"]
                            }
                        }
                    }
                ]
            };

            var dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(postmanCollection, null, 2));
            var downloadAnchorNode = document.createElement('a');
            downloadAnchorNode.setAttribute("href", dataStr);
            downloadAnchorNode.setAttribute("download", "api-reseller-system.postman_collection.json");
            document.body.appendChild(downloadAnchorNode);
            downloadAnchorNode.click();
            downloadAnchorNode.remove();

            $scope.$parent.showAlert('Postman collection downloaded!', 'success');
        };
    }]);
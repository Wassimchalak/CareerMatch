import axios from "axios";

const api = axios.create({
    baseURL: import.meta.env.VITE_API_URL,
});

function clearAuthenticationData() {
    localStorage.removeItem("token");
    localStorage.removeItem("userId");
    localStorage.removeItem("fullName");
    localStorage.removeItem("email");
    localStorage.removeItem("expiresAt");

    sessionStorage.removeItem(
        "careerMatchLatestSearchJobs"
    );

    sessionStorage.removeItem(
        "careerMatchLatestSearchForm"
    );

    sessionStorage.removeItem(
        "careerMatchHasSearched"
    );

    sessionStorage.removeItem(
        "careerMatchRevealedMatchJobIds"
    );
}

function isPublicAuthRequest(
    requestUrl: string
) {
    return (
        requestUrl.includes(
            "/Auth/login"
        ) ||
        requestUrl.includes(
            "/Auth/register"
        ) ||
        requestUrl.includes(
            "/Auth/forgot-password"
        ) ||
        requestUrl.includes(
            "/Auth/reset-password"
        )
    );
}

api.interceptors.request.use(
    (config) => {
        const requestUrl =
            config.url || "";

        /*
         * Login, register, forgot-password,
         * and reset-password must always be
         * allowed to reach the backend.
         *
         * An expired token from an old session
         * should never block these requests.
         */
        if (
            isPublicAuthRequest(
                requestUrl
            )
        ) {
            return config;
        }

        const token =
            localStorage.getItem(
                "token"
            );

        const expiresAt =
            localStorage.getItem(
                "expiresAt"
            );

        /*
         * Only check expiration when there
         * is actually a stored token.
         */
        if (
            token &&
            expiresAt
        ) {
            const expirationDate =
                new Date(
                    expiresAt
                );

            const expirationTime =
                expirationDate.getTime();

            const tokenHasValidExpirationDate =
                !Number.isNaN(
                    expirationTime
                );

            const tokenHasExpired =
                tokenHasValidExpirationDate &&
                expirationTime <=
                    Date.now();

            if (
                tokenHasExpired
            ) {
                clearAuthenticationData();

                /*
                 * Avoid repeatedly replacing
                 * the page when already on
                 * the authentication page.
                 */
                if (
                    window.location.pathname !==
                    "/auth"
                ) {
                    window.location.replace(
                        "/auth"
                    );
                }

                return Promise.reject(
                    new Error(
                        "Your session has expired."
                    )
                );
            }
        }

        /*
         * Add the JWT only to requests that
         * require authentication.
         */
        if (token) {
            config.headers.Authorization =
                `Bearer ${token}`;
        }

        return config;
    },

    (error) => {
        return Promise.reject(
            error
        );
    }
);

api.interceptors.response.use(
    (response) => {
        return response;
    },

    (error) => {
        const status =
            error.response?.status;

        const requestUrl =
            error.config?.url ||
            "";

        const publicAuthRequest =
            isPublicAuthRequest(
                requestUrl
            );

        /*
         * A 401 from a protected endpoint
         * means the current authentication
         * session is no longer valid.
         *
         * Do not apply this behavior to
         * login/register/password endpoints,
         * because a 401 there simply means
         * the authentication attempt failed.
         */
        if (
            status === 401 &&
            !publicAuthRequest
        ) {
            clearAuthenticationData();

            if (
                window.location.pathname !==
                "/auth"
            ) {
                window.location.replace(
                    "/auth"
                );
            }
        }

        return Promise.reject(
            error
        );
    }
);

export default api;
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

api.interceptors.request.use(
    (config) => {
        const token =
            localStorage.getItem("token");

        const expiresAt =
            localStorage.getItem("expiresAt");

        if (expiresAt) {
            const expirationDate =
                new Date(expiresAt);

            const tokenHasExpired =
                !Number.isNaN(
                    expirationDate.getTime()
                ) &&
                expirationDate.getTime() <=
                    Date.now();

            if (tokenHasExpired) {
                clearAuthenticationData();

                window.location.replace(
                    "/auth"
                );

                return Promise.reject(
                    new Error(
                        "Your session has expired."
                    )
                );
            }
        }

        if (token) {
            config.headers.Authorization =
                `Bearer ${token}`;
        }

        return config;
    },
    (error) => {
        return Promise.reject(error);
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
            error.config?.url || "";

        const isLoginRequest =
            requestUrl.includes(
                "/Auth/login"
            );

        const isRegisterRequest =
            requestUrl.includes(
                "/Auth/register"
            );

        const isForgotPasswordRequest =
            requestUrl.includes(
                "/Auth/forgot-password"
            );

        const isResetPasswordRequest =
            requestUrl.includes(
                "/Auth/reset-password"
            );

        const isPublicAuthRequest =
            isLoginRequest ||
            isRegisterRequest ||
            isForgotPasswordRequest ||
            isResetPasswordRequest;

        if (
            status === 401 &&
            !isPublicAuthRequest
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

        return Promise.reject(error);
    }
);

export default api;
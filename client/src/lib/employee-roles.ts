export const personRoleIds = (person?: {employeeRoleIds?: string[]; employeeRoleId?: string}) => person?.employeeRoleIds ?? (person?.employeeRoleId ? [person.employeeRoleId] : []);
